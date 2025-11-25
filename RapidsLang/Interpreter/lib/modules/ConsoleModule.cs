using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Types;

namespace RapidsLang.Interpreter.Lib.Modules;

public class ConsoleModule : Module
{
    private static void Print(RapidsInterpreter interpreter)
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
    
    private static readonly RapidsType PrintType = new RapidsFunctionType(
        [new("value", RapidsAnyType.Instance)],
        null
    );

    private static void Input(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        if (ctx.FunctionCallStack.TryPop(out var value))
        {
            Console.Write(Utils.StringifyVariable(value));
        }
        
        var input = Console.ReadLine();
        ctx.FunctionCallStack.Push(input != null ? new RapidsStringVariable(input) : new RapidsNullVariable());
    }

    private static readonly RapidsType InputType = new RapidsFunctionType(
        [],
        RapidsPrimitiveType.String
    );

    private static void Write(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        ctx.FunctionCallStack.TryPop(out var variable);

        if (variable is not null)
        {
            Console.Write(Utils.StringifyVariable(variable));
        }
    }

    private static readonly RapidsType WriteType = new RapidsFunctionType(
        [new("value", RapidsAnyType.Instance)],
        null
    );

    public override ModuleExports Exports { get; } = new(new Dictionary<string, ModuleExport> {
        {"print", new(RapidsFunctionReferenceVariable.OfNative(Print, PrintType), PrintType)},
        {"input", new(RapidsFunctionReferenceVariable.OfNative(Input, InputType), PrintType)},
        {"write", new(RapidsFunctionReferenceVariable.OfNative(Write, WriteType), PrintType)}
    });
}