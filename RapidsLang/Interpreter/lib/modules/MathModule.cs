using RapidsLang.Interpreter.Variables;

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

    protected override ModuleExports Exports { get; } = new(new()
    {
        {"round", RapidsFunctionReferenceVariable.ofNative(Round)},
    });
}