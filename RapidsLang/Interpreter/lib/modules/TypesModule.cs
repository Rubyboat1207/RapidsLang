using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class TypesModule : Module
{
    private static void ParseNumber(RapidsInterpreter interpreter)
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var strVar) && strVar is RapidsStringVariable str)
        {
            if (double.TryParse(str.Value, out var result))
            {
                interpreter.Context.FunctionCallStack.Push(new RapidsNumberVariable(result));
                return;
            }
            
        }
        interpreter.Context.FunctionCallStack.Push(new RapidsNullVariable());
    }

    private static readonly RapidsType ParseNumberType = new RapidsFunctionType(
        [new("value", RapidsStringType.Instance)],
        RapidsPrimitiveType.Number
    );

    private static void Typeof(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();


        var variable = utils.LatestVariable() ?? new RapidsNullVariable();
        utils.Return(variable.VariableTypeName);
    }

    private static readonly RapidsType TypeofType = new RapidsFunctionType(
        [new("type", RapidsAnyType.Instance)],
        RapidsStringType.Instance
    );

    public override ModuleExports Exports { get; } = new(new Dictionary<string, ModuleExport>
    {
        {"parseNumber", new(RapidsFunctionReferenceVariable.OfNative(ParseNumber, ParseNumberType), ParseNumberType)},
        {"typeof", new(RapidsFunctionReferenceVariable.OfNative(Typeof, TypeofType), TypeofType)}
    });
}