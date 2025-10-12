using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record ResumeExecutionWork(BlockType Type, RapidsInterpreter Interpreter) : InterpreterWork(Interpreter)
{
    private bool _done;
    public override void Execute()
    {
        Interpreter.WorkStack.Pop(); // just pop myself off.
        Interpreter.CollapseStack();
        while (Interpreter.WorkStack.TryPeek(out var work))
        {
            ActiveNode = work.ActiveNode;
            if (work is not CodeBlockRunWork block)
            {
                Interpreter.WorkStack.Pop();
                continue;
            }

            if (block.Scope.BlockType == Type)
            {
                break;
            }
        }

        _done = true;
    }

    public override bool IsDone()
    {
        return _done;
    }

    public override Node ActiveNode { get; protected set; }
}