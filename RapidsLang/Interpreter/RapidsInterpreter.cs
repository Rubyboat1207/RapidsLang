using RapidsLang.Extensions;
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
    Statement
}

public class BlockProgress(StatementsNode block, BlockType blockType, int programCounter=0)
{
    public int ProgramCounter { get; set; } = programCounter;
    public StatementsNode Block { get; } = block;
    public RapidsVariable? Return = null;
    public BlockType BlockType { get; } = blockType;
    public bool ShouldBreakOut { get; set; }
}

public class RapidsInterpreter
{
    private readonly string _sourceCode;
    private readonly RapidsPreprocMetaData _preprocessorMetadata;

    public RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, string? mainSourceCodePath=null)
    {
        _sourceCode = sourceCode;
        _preprocessorMetadata = preprocessorMetadata;

        MainSourceCodePath = mainSourceCodePath;
        
        ContextStack.Push(new InterpreterContext(sourceCode, preprocessorMetadata, mainSourceCodePath));
    }

    public Stack<InterpreterContext> ContextStack { get; } = new();
    public InterpreterContext Context => ContextStack.Peek();
    public Stack<InterpreterWork> WorkStack { get; }  = [];
    public string? MainSourceCodePath { get; }

    public void PushWork(InterpreterWork work)
    {
        WorkStack.Push(work);
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

    public void Interpret(StatementsNode root)
    {
        StartNewBlock(root, BlockType.Module, null);
        while (true)
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
                break;
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

    public void AddExtensionModule(ExtensionData extensionData)
    {
        
    }
}