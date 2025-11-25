using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class EnvironmentModule : Module
{
    public override ModuleExports Exports { get; } = new(new()
    {
        {"getVar", new(RapidsFunctionReferenceVariable.OfNative(GetVar, GetVarType), GetVarType)}
    });

    private static readonly RapidsType GetVarType = new RapidsFunctionType(
        [new("name", RapidsStringType.Instance)],
        RapidsStringType.Instance
    );
    private static void GetVar(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil().GuaranteeReturn();

        var str = util.LatestString();

        if (str is null)
        {
            return;
        }
        
        util.Return(Environment.GetEnvironmentVariable(str.Value));
    }
}