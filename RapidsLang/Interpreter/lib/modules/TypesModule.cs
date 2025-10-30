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

    private static void Typeof(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();


        var variable = utils.LatestVariable() ?? new RapidsNullVariable();
        utils.Return(variable.VariableTypeName);
    }
    
    protected override ModuleExports Exports { get; } = new(new Dictionary<string, RapidsVariable>
    {
        {"parseNumber", RapidsFunctionReferenceVariable.ofNative(ParseNumber)},
        {"typeof", RapidsFunctionReferenceVariable.ofNative(Typeof)}
    });
}