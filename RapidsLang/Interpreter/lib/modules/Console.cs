namespace RapidsLang.Interpreter.Lib.Modules;

public class ConsoleModule : Module
{
    public static RapidsFunctionResult Print(InterpreterContext context)
    {
        context.FunctionCallStack.TryPop(out var variable);

        if (variable is null)
        {
            Console.WriteLine();
        }else
        {
            Console.WriteLine(Utils.StringifyVariable(variable));
        }

        return RapidsFunctionResult.VoidRet();
    }

    public override void Import(InterpreterContext context)
    {
        context.AddExternalFunction("print", Print);
    }
}