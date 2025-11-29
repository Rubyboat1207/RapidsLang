using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record NumericForLoopRunWork : InterpreterWork
{
    private double _index;
    private RapidsNumberVariable Start { get; }
    private RapidsNumberVariable End { get; }
    private RapidsNumberVariable? StepSize { get; }
    
    private NumericForLoop Loop { get; }
    
    public NumericForLoopRunWork(
        RapidsInterpreter Interpreter,
        CodeBlockRunWork? Parent,
        NumericForLoop loop,
        RapidsNumberVariable start,
        RapidsNumberVariable end,
        RapidsNumberVariable? stepSize
    ) : base(Interpreter, Parent)
    {
        _index = start.Value;
        Start = start;
        End = end;
        StepSize = stepSize;
        Loop = loop;

    }

    public override void Execute()
    {
        var context = new InterpreterContext(Context);
        context.AddVariable(Loop.Index.Value, new VariableHolder(new RapidsNumberVariable(_index), false));
        var block = Interpreter.StartNewBlock(Loop.Body, BlockType.Loop, Parent, context);

        block.OnCompleted += OnForBlockCompleted;
    }
    
    private void OnForBlockCompleted(InterpreterWork work)
    {
        if (End.Value > Start.Value)
        {
            _index += StepSize?.Value ?? 1;
        }
        else
        {
            _index -= StepSize?.Value ?? 1;
        }
        work.OnCompleted -= OnForBlockCompleted;
    }

    public override bool IsDone()
    {
        if (Math.Abs(End.Value - Start.Value) < 0.000001)
        {
            return true;
        }
        if (Loop.IncludesEnd)
        {
            if (End.Value > Start.Value)
            {
                return _index >= End.Value;
            }

            return _index <= End.Value;
        }

        if (End.Value > Start.Value)
        {
            return _index > End.Value;
        }

        return _index < End.Value;
    }

    public override Node ActiveNode => Loop;
}