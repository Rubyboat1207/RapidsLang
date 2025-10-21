using RapidsLang.Interpreter.Work;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public abstract class RapidsFunction
{
    public event Action? OnCompleted = null;
    public virtual void EnqueueExecution(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        MarkComplete();
    }

    protected void MarkComplete()
    {
        OnCompleted?.Invoke();
    }
}

public class RapidsNativeFunction(Action<RapidsInterpreter> func) : RapidsFunction
{
    public Action<RapidsInterpreter> Function { get; } = func;
    public override void EnqueueExecution(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        Function.Invoke(interpreter);

        base.EnqueueExecution(interpreter, parentCodeBlock);
    }
}

public class RapidsUserFunction(FunctionNode func)  : RapidsFunction
{
    public FunctionNode Func { get; } = func;
    public override void EnqueueExecution(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        var ctx = interpreter.Context;
        var body = interpreter.StartNewBlock(Func.Body, BlockType.Function, parentCodeBlock);

        var oldVariables = new List<Tuple<string, VariableHolder>>();
        
        if (Func.Arguments is not null)
        {
            foreach(var arg in Func.Arguments.ToArray().Reverse())
            {
                var name = arg.Name.Value;
                body.Scope.ScopedVariables.Add(name);
                if (ctx.variables.TryGetValue(name, out var value))
                {
                    oldVariables.Add(new Tuple<string, VariableHolder>(name, value));
                    ctx.variables.Remove(name);
                }
                ctx.variables.Add(name, new VariableHolder(ctx.FunctionCallStack.Pop(), false, arg.Type ?? null));
            }
        }

        body.OnCompleted += (_) =>
        {
            foreach (var oldVariable in oldVariables)
            {
                ctx.variables.Add(oldVariable.Item1, oldVariable.Item2);
            }

            if (body.Scope.Return is not null)
            {
                ctx.FunctionCallStack.Push(body.Scope.Return);
            }

            base.EnqueueExecution(interpreter, parentCodeBlock);

        };
        
    }
}