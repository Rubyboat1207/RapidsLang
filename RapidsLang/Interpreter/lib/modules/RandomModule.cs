using RapidsLang.Analyzer.Types;
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

    private static readonly RapidsType RangeType = new RapidsFunctionType(
        [new("min", RapidsPrimitiveType.Number), new("max", RapidsPrimitiveType.Number)],
        RapidsPrimitiveType.Number
    );

    public static void RandomNumber(RapidsInterpreter interpreter)
    {
        interpreter.Context.FunctionCallStack.Push(new RapidsNumberVariable(Random.Shared.NextDouble()));
    }

    private static readonly RapidsType RandomNumberType = new RapidsFunctionType(
        [],
        RapidsPrimitiveType.Number
    );

    public override ModuleExports Exports { get; } = new(new()
    {
        {"range", new(RapidsFunctionReferenceVariable.OfNative(Range, RangeType), RangeType)},
        {"randomNumber", new(RapidsFunctionReferenceVariable.OfNative(RandomNumber, RandomNumberType), RandomNumberType)}
    });
}