using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;
using RapidsLang.InterpreterVM;

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
    
    private static void PrintVm(Frame frame)
    {
        if (frame.Stack.TryPop(out var variable))
        {
            Console.WriteLine(Utils.StringifyVariable(variable));
        }else
        {
            Console.WriteLine();
        }
        
        frame.Stack.Push(new RapidsBooleanVariable(false));
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
        RapidsStringType.Instance
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
        {"print", new(RapidsFunctionReferenceVariable.OfNativeWithVm(Print, PrintVm, 1, PrintType), PrintType)},
        {"input", new(RapidsFunctionReferenceVariable.OfNative(Input, InputType), PrintType)},
        {"write", new(RapidsFunctionReferenceVariable.OfNative(Write, WriteType), PrintType)}
    });
}