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

    public override void Import(InterpreterContext context)
    {
        context.AddNativeFunction("print", Print);
    }
}