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
}

public class RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, StatementsNode root)
{
    public InterpreterContext Context { get; } = new();
    public Stack<InterpreterWork> WorkStack { get; }  = [];

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

    public void Interpret()
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
                    throw new Exception($"At {GetLineCol(work.ActiveNode.BaseToken)}", e);
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
            work.CompletedListeners.ForEach(l => l.Invoke());
            continue;
        }
    }
}