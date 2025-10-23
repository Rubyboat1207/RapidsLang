using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class TimeModule : Module
{
    public static void Sleep(RapidsInterpreter interpreter)
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var msVariable) && msVariable is RapidsNumberVariable number)
        {
            Thread.Sleep((int) number.Value);
        }
    }
    
    protected override ModuleExports Exports { get; } = new ModuleExports(new()
    {
        {"sleep", RapidsFunctionReferenceVariable.ofNative(Sleep)},
    });
}