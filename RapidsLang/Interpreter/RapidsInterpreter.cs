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
    public readonly List<string> ScopedVariables = [];
    // Todo: Closures
    public StatementsNode Block { get; } = block;
    public RapidsVariable? Return = null;
    public BlockType BlockType { get; } = blockType;
    public bool ShouldBreakOut { get; set; }
}

public class RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, string? mainSourceCodePath=null)
{
    public InterpreterContext Context { get; } = new();
    public Stack<InterpreterWork> WorkStack { get; }  = [];
    public string? MainSourceCodePath { get; } = mainSourceCodePath;

    public void PushWork(InterpreterWork work)
    {
        WorkStack.Push(work);
    }

    public string GetLineCol(Token token)
    {
        return RapidsPreproc.GetRowCol(sourceCode, token.Index, preprocessorMetadata);
    }

    public CodeBlockRunWork StartNewBlock(StatementsNode block, BlockType type, CodeBlockRunWork? Parent)
    {
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
                        throw new Exception($"At {mainSourceCodePath} {GetLineCol(work.ActiveNode.BaseToken)}", e);
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
    }

    public void AddExtensionModule(ManifestContainer manifestContainer)
    {
        
    }
}