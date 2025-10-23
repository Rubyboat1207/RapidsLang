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

public class RapidsNativeFunctionWithCodeBlock(Action<RapidsInterpreter, CodeBlockRunWork?> func) : RapidsFunction
{
    public Action<RapidsInterpreter, CodeBlockRunWork?> Function { get; } = func;
    public override void EnqueueExecution(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        Function.Invoke(interpreter, parentCodeBlock);

        base.EnqueueExecution(interpreter, parentCodeBlock);
    }
}

public class RapidsUserFunction(FunctionNode func, InterpreterContext closure)  : RapidsFunction
{
    public FunctionNode Func { get; } = func;
    public override void EnqueueExecution(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        var ctx = interpreter.Context;
        var newContext = closure.Clone();
        
        if (Func.Arguments is not null)
        {
            foreach(var arg in Func.Arguments.ToArray().Reverse())
            {
                var name = arg.Name.Value;
                newContext.AddVariable(name, new VariableHolder(ctx.FunctionCallStack.Pop(), false, arg.Type ?? null));
            }
        }
        
        var body = interpreter.StartNewBlock(Func.Body, BlockType.Function, parentCodeBlock, newContext);


        body.OnCompleted += (_) =>
        {
            if (body.Scope.Return is not null)
            {
                ctx.FunctionCallStack.Push(body.Scope.Return);
            }

            base.EnqueueExecution(interpreter, parentCodeBlock);
        };
        
    }
}