using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record IterativeForLoopRunWork : InterpreterWork
{
    private int _index;
    private List<(RapidsVariable, RapidsVariable)> List { get; }
    
    private IterativeForLoop Loop { get; }
    
    public IterativeForLoopRunWork(
        RapidsInterpreter Interpreter,
        CodeBlockRunWork? Parent,
        IterativeForLoop loop,
        List<(RapidsVariable, RapidsVariable)> list
    ) : base(Interpreter, Parent)
    {
        Loop = loop;
        List = list;
    }

    public override void Execute()
    {
        var context = new InterpreterContext(Context);
        
        context.AddVariable(Loop.Item.Value, new VariableHolder(List[_index].Item2, false));
        if (Loop.Index is not null)
        {
            context.AddVariable(Loop.Index.Value, new VariableHolder(List[_index].Item1, false));
        }
        var block = Interpreter.StartNewBlock(Loop.Body, BlockType.Loop, Parent, context);

        block.OnCompleted += OnForBlockCompleted;
    }
    
    private void OnForBlockCompleted(InterpreterWork work)
    {
        _index += 1;
        work.OnCompleted -= OnForBlockCompleted;
    }

    public override bool IsDone()
    {
        return _index >= List.Count;
    }

    public override Node ActiveNode => Loop;
}