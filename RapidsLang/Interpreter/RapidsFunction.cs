using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public abstract class RapidsFunction
{
    public abstract void EnqueueExecution(InterpreterContext ctx);
}

public class RapidsNativeFunction(Action<InterpreterContext> func) : RapidsFunction
{
    public Action<InterpreterContext> Function { get; } = func;
    public override void EnqueueExecution(InterpreterContext ctx)
    {
        Function.Invoke(ctx);
    }
}

public class RapidsUserFunction(FunctionNode func, RapidsInterpreter interpreter)  : RapidsFunction
{
    public FunctionNode Func { get; } = func;
    public RapidsInterpreter Interpreter { get; } = interpreter;

    public override void EnqueueExecution(InterpreterContext ctx)
    {
        var body = Interpreter.StartNewBlock(func.Body);

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
        
        body.CompletedListeners.Add(() =>
        {
            foreach (var oldVariable in oldVariables)
            {
                ctx.variables.Add(oldVariable.Item1, oldVariable.Item2);
            }

            if (body.Scope.Return is not null)
            {
                ctx.FunctionCallStack.Push(body.Scope.Return);
            }
        });
    }
}