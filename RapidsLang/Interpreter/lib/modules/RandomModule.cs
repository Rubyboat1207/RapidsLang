using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class RandomModule : Module
{
    public static void Range(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();
        
        var a = utils.LatestNumber();
        var b = utils.LatestNumber();

        if (a == null || b == null)
        {
            return;
        }

        var min = Math.Min(a.Value, b.Value);
        var max = Math.Max(a.Value, b.Value);

        utils.Return(Random.Shared.NextDouble() * (max - min) + min);
    }

    public static void RandomNumber(RapidsInterpreter interpreter)
    {
        interpreter.Context.FunctionCallStack.Push(new RapidsNumberVariable(Random.Shared.NextDouble()));
    }

    protected override ModuleExports Exports { get; } = new(new()
    {
        {"range", RapidsFunctionReferenceVariable.ofNative(Range)},
        {"randomNumber", RapidsFunctionReferenceVariable.ofNative(RandomNumber)}
    });
}