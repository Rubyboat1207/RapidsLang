using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class ConsoleModule : Module
{
    public static void Print(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        ctx.FunctionCallStack.TryPop(out var variable);

        if (variable is null)
        {
            Console.WriteLine();
        }else
        {
            Console.WriteLine(Utils.StringifyVariable(variable));
        }
    }

    public static void Input(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        if (ctx.FunctionCallStack.TryPop(out var value))
        {
            Console.Write(Utils.StringifyVariable(value));
        }
        
        var input = Console.ReadLine();
        ctx.FunctionCallStack.Push(input != null ? new RapidsStringVariable(input) : new RapidsNullVariable());
    }

    public static void PutChar(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        ctx.FunctionCallStack.TryPop(out var variable);

        if (variable is not null)
        {
            Console.Write(Utils.StringifyVariable(variable));
        }
    }

    protected override ModuleExports Exports { get; } = new(new Dictionary<string, RapidsVariable> {
        {"print", RapidsFunctionReferenceVariable.ofNative(Print)},
        {"putChar", RapidsFunctionReferenceVariable.ofNative(PutChar)},
        {"input", RapidsFunctionReferenceVariable.ofNative(Input)},
    });
}