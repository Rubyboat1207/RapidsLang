using System.Collections.Concurrent;
using RapidsLang.Extension.Communication.Native;
using RapidsLang.Extensions;
using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter.Lib.Modules;
using RapidsLang.Interpreter.Variables;
using RapidsLang.Interpreter.Work;
using RapidsLang.Lexer;
using RapidsLang.PreProcessor;

namespace RapidsLang.Interpreter;

using Parser.Nodes;

public enum BlockType
{
    Module,
    Function,
    Loop,
    SourceCallback,
    Statement
}

public class BlockProgress(StatementsNode block, BlockType blockType, int programCounter=0)
{
    public int ProgramCounter { get; set; } = programCounter;
    public StatementsNode Block { get; } = block;
    public BlockType BlockType { get; } = blockType;
    // only used for the "on" statement's callback. Probably a better way to do this.
    public RapidsDataChannelVariable? Source;
    public Guid SourceSubscriptionId;
    public bool ShouldBreakOut { get; set; }
}

public class RapidsInterpreter
{
    public readonly NativeProtocol NativeProtocol;

    private bool _done;

    private bool Done
    {
        get => _done;
        set
        {
            _done = value;
            if (_done)
            {
                WakeUp();
            }
        }
    }
    public bool SupportsOnStatements { get; set; }

    public RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, string? mainSourceCodePath=null, bool supportsOnStatements=false, NativeProtocol? nativeProtocol=null)
    {
        MainSourceCodePath = mainSourceCodePath;
        
        ContextStack.Push(new InterpreterContext(sourceCode, preprocessorMetadata, mainSourceCodePath));

        SupportsOnStatements = supportsOnStatements;
        NativeProtocol = nativeProtocol ?? new();
    }

    private Stack<InterpreterContext> ContextStack { get; } = new();
    public InterpreterContext Context => ContextStack.Peek();
    private Stack<InterpreterWork> WorkStack { get; }  = [];
    public string? MainSourceCodePath { get; }
    private readonly SemaphoreSlim _workSignal = new(0);
    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private bool _isExiting;
    private static readonly Lock ExitLock = new();

    public void WakeUp()
    {
        if (_workSignal.CurrentCount == 0)
        {
            // Console.WriteLine("should wake up interpreter: " + GetHashCode());
            _workSignal.Release();
        }
    }

    public async Task<RapidsVariable?> InterpretExpressionAndThenDie(ExpressionNode expressionNode)
    {
        RapidsVariable? variable = null;
        EvaluateExpression(expressionNode, (value) =>
        {
            Done = true;
            variable = value;
        }, null);

        await InterpretLoop(false);

        return variable;
    }
    

    public void PushWork(InterpreterWork work)
    {
        WorkStack.Push(work);
        WakeUp();
    }

    public string GetLineCol(Token token)
    {
        return RapidsPreproc.GetRowCol(Context.SourceCode, token.Index, Context.PreprocMetaData);
    }

    public CodeBlockRunWork StartNewBlock(StatementsNode block, BlockType type, CodeBlockRunWork? parent, InterpreterContext? ctx=null)
    {
        ContextStack.Push(ctx ?? new InterpreterContext(Context));
        CollapseStack();
        var progress = new BlockProgress(block, type);
        var work = new CodeBlockRunWork(progress, this, parent);
        WorkStack.Push(work);

        return work;
    }

    public async Task Interpret(StatementsNode root, bool topLevel=false)
    {
        if (topLevel)
        {
            NativeProtocol.Init(this, null);
        }
        StartNewBlock(root, BlockType.Module, null);
        Done = false;
        await InterpretLoop(topLevel);
    }

    private async Task InterpretLoop(bool allowSleeping)
    {
        while (!Done)
        {
            CollapseStack();
            if (WorkStack.TryPeek(out var work))
            {
                try
                {
                    work.Execute();
                }
                catch(Exception e)
                {
                    if (work.ActiveNode is not null)
                    {
                        string message = "An error occurred";
                        try
                        {
                            message = $"At {Context.SourcePath} {GetLineCol(work.ActiveNode.BaseToken)}";
                        }
                        catch
                        {
                            // ignored
                        }
                        throw new Exception(message, e);
                    }

                    throw;
                }
            }
            else
            {
                if (!allowSleeping)
                {
                    break;
                }
                // Console.WriteLine("interpreter: " + GetHashCode() + " is going to sleep");
                await _workSignal.WaitAsync();
                ExecutePendingActions();
                Context.ModuleRegistry.TickExternalModules(Context);
                // Console.WriteLine("interpreter: " + GetHashCode() + " is waking up");
            }
        }
    }

    private void CollapseStack()
    {
        while(WorkStack.TryPeek(out var work) && work.IsDone())
        {
            WorkStack.Pop().Cleanup();
        }

        while (ContextStack.TryPeek(out var ctx) && !ctx.Active)
        {
            ContextStack.Pop();
        }
    }

    public static void Exit(RapidsInterpreter interpreter)
    {
        interpreter.Done = true;
    }

    public static void InPrimaryModule(RapidsInterpreter interpreter)
    {
        interpreter.Context.FunctionCallStack.Push(new RapidsBooleanVariable(interpreter.Context.CurrentModule is null));
    }
    
    public InterpreterNativeFunctionUtil GetNativeUtil()
    {
        return new InterpreterNativeFunctionUtil(this);
    }
    
    public void EnqueueAction(Action action)
    {
        _pendingActions.Enqueue(action);
        WakeUp();
    }

    private void ExecutePendingActions()
    {
        while (_pendingActions.TryDequeue(out var action))
        {
            try 
            {
                action();
            }
            catch (Exception e)
            {
                // Log or handle critical failures in callbacks
                Console.WriteLine($"Error in pending action: {e.Message}");
            }
        }
    }

    public async Task HandleExit()
    {
        lock (ExitLock)
        {
            if (_isExiting)
            {
                return;
            }
            _isExiting = true;
        }
        
        _pendingActions.Clear();
        var outermostContext = ContextStack.ToArray().FirstOrDefault();
        while (ContextStack.TryPeek(out var _))
        {
            ContextStack.Pop();
        }
        
        // The idea here is that if this fails, we'll pass in
        if (outermostContext is not null)
        {
            ContextStack.Push(outermostContext);
        }
        
        WorkStack.Clear();

        NativeProtocol.WriteToOutput(ProgramModule.SigintIdent, new RapidsBooleanVariable(outermostContext is null));

        await InterpretLoop(false);
    }
    
    public void EvaluateExpression(ExpressionNode expressionNode, Action<RapidsVariable> callback, CodeBlockRunWork? parent)
    {
        if (expressionNode is FunctionCallExpressionNode fcen)
        { 
            PushWork(new FunctionCallExpressionEvaluateWork(fcen, callback, this, parent));
            return;
        }
        if (expressionNode is StringNode str)
        {
            PushWork(new StringExpressionEvaluateWork(str, callback, this, parent));
            return;
        }
        PushWork(new DefaultExpressionEvaluateWork(expressionNode, callback, this, parent));
    }

    public void EvaluateExpressions(List<ExpressionNode> expressionNodes, Action<List<RapidsVariable>> callback, CodeBlockRunWork parent)
    {
        List<RapidsVariable> variables = [];

        for (var i = 0; i < expressionNodes.Count; i++)
        {
            var node = expressionNodes[expressionNodes.Count - 1 - i];

            EvaluateExpression(node, v =>
            {
                variables.Add(v);

                if (variables.Count == expressionNodes.Count)
                {
                    callback.Invoke(variables);
                }
            }, parent);
        }
        if(expressionNodes.Count == 0)
        {
            callback.Invoke(variables);
        }
    }
}