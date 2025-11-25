using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Types;

namespace RapidsLang.Interpreter.Lib.Modules;

public class MathModule : Module
{
    private static void Round(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();
        
        var a = utils.LatestNumber();

        if (a == null)
        {
            return;
        }

        utils.Return(Math.Round(a.Value));
    }

    private static readonly RapidsType RoundType = new RapidsFunctionType(
        [new("a", RapidsPrimitiveType.Number)],
        RapidsPrimitiveType.Number
    );

    public override ModuleExports Exports { get; } = new(new()
    {
        {"round", new(RapidsFunctionReferenceVariable.OfNative(Round, RoundType), RoundType)},
    });
}