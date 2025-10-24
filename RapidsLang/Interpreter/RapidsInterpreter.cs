using RapidsLang.Extensions;
using RapidsLang.Extensions.Pipes;
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
    public RapidsVariable? Return = null;
    public BlockType BlockType { get; } = blockType;
    // only used for the "on" statement's callback. Probably a better way to do this.
    public RapidsDataChannelVariable? Source;
    public Guid SourceSubscriptionId;
    public bool ShouldBreakOut { get; set; }
}

public class RapidsInterpreter
{
    private readonly string _sourceCode;
    private readonly RapidsPreprocMetaData _preprocessorMetadata;

    private bool _done;
    public bool Done
    {
        private get => _done;
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

    public RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, string? mainSourceCodePath=null, bool supportsOnStatements=false)
    {
        _sourceCode = sourceCode;
        _preprocessorMetadata = preprocessorMetadata;

        MainSourceCodePath = mainSourceCodePath;
        
        ContextStack.Push(new InterpreterContext(sourceCode, preprocessorMetadata, mainSourceCodePath));

        SupportsOnStatements = supportsOnStatements;
    }

    public Stack<InterpreterContext> ContextStack { get; } = new();
    public InterpreterContext Context => ContextStack.Peek();
    private Stack<InterpreterWork> WorkStack { get; }  = [];
    public string? MainSourceCodePath { get; }
    private readonly SemaphoreSlim _workSignal = new SemaphoreSlim(0);

    public void WakeUp()
    {
        if (_workSignal.CurrentCount == 0)
        {
            // Console.WriteLine("should wake up interpreter: " + GetHashCode());
            _workSignal.Release();
        }
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

    public CodeBlockRunWork StartNewBlock(StatementsNode block, BlockType type, CodeBlockRunWork? Parent, InterpreterContext? ctx=null)
    {
        ContextStack.Push(ctx ?? new InterpreterContext(Context));
        CollapseStack();
        var progress = new BlockProgress(block, type);
        var work = new CodeBlockRunWork(progress, this, Parent);
        WorkStack.Push(work);

        return work;
    }

    public async Task Interpret(StatementsNode root, bool topLevel=false)
    {
        StartNewBlock(root, BlockType.Module, null);
        Done = false;
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
                if (!topLevel)
                {
                    break;
                }
                // Console.WriteLine("interpreter: " + GetHashCode() + " is going to sleep");
                await _workSignal.WaitAsync();
                Context.ModuleRegistry.TickExternalModules(Context);
                // Console.WriteLine("interpreter: " + GetHashCode() + " is waking up");
            }

        }
    }

    public void CollapseStack()
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
}