using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Work;

namespace RapidsLang.Interpreter.Variables;

public class RapidsFunctionReferenceVariable(RapidsFunction function) : RapidsVariable
{
    public override string VariableTypeName => "function";
    public override bool Truthy => true;
    public RapidsFunction Function { get; init; } = function;
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsStringVariable rString)
        {
            return new RapidsStringVariable(Utils.StringifyVariable(this) + rString.Value);
        }
        
        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            var result = other is RapidsFunctionReferenceVariable oFunc && oFunc.Function == Function;

            if (op is RapidsOperator.Inequal)
            {
                result = !result;
            }

            return new RapidsBooleanVariable(result);
        }

        return null;
    }
    
    public override RapidsVariable ShallowCopy()
    {
        return new RapidsFunctionReferenceVariable(Function);
    }
    
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

    private static Dictionary<Action<RapidsInterpreter>, RapidsNativeFunction> NativeFunctions = [];
    private static Dictionary<Action<RapidsInterpreter, CodeBlockRunWork?>, RapidsNativeFunctionWithCodeBlock> NativeFunctionsWithCodeBlocks = [];

    public static RapidsFunctionReferenceVariable OfNative(Action<RapidsInterpreter> nativeFunc, RapidsType? type=null)
    {
        if (!NativeFunctions.TryGetValue(nativeFunc, out var value))
        {
            value = new RapidsNativeFunction(nativeFunc, type);
            NativeFunctions[nativeFunc] = value;
        }
        
        return new RapidsFunctionReferenceVariable(value);
    }
    
    public static RapidsFunctionReferenceVariable OfNative(Action<RapidsInterpreter, CodeBlockRunWork?> nativeFunc, RapidsType? type=null)
    {
        if (!NativeFunctionsWithCodeBlocks.TryGetValue(nativeFunc, out var value))
        {
            value = new RapidsNativeFunctionWithCodeBlock(nativeFunc, type);
            NativeFunctionsWithCodeBlocks[nativeFunc] = value;
        }
        
        return new RapidsFunctionReferenceVariable(value);
    }
    
    public override List<(RapidsVariable, RapidsVariable)>? GetIterable() => null;
}