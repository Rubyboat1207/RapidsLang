using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class ConsoleModule : Module
{
    public static void Print(InterpreterContext context)
    {
        context.FunctionCallStack.TryPop(out var variable);

        if (variable is null)
        {
            Console.WriteLine();
        }else
        {
            Console.WriteLine(Utils.StringifyVariable(variable));
        }
    }

    public static void Input(InterpreterContext context)
    {
        if (context.FunctionCallStack.TryPop(out var value))
        {
            Console.Write(Utils.StringifyVariable(value));
        }
        
        var input = Console.ReadLine();
        context.FunctionCallStack.Push(input != null ? new RapidsStringVariable(input) : new RapidsNullVariable());
    }

    public static void PutChar(InterpreterContext ctx)
    {
        ctx.FunctionCallStack.TryPop(out var variable);

        if (variable is not null)
        {
            Console.Write(Utils.StringifyVariable(variable));
        }
    }

    public override void Import(InterpreterContext context)
    {
        context.AddNativeFunction("print", Print);
        context.AddNativeFunction("putChar", PutChar);
        context.AddNativeFunction("input", Input);
    }
}