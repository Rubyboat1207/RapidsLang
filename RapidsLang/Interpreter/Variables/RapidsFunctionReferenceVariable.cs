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
}