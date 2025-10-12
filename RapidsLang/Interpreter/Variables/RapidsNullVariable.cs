namespace RapidsLang.Interpreter.Variables;

public class RapidsNullVariable : RapidsVariable
{
    public override string VariableTypeName => "null";
    public override bool Truthy => false;

    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsStringVariable rString)
        {
            return new RapidsStringVariable("null" + rString.Value);
        }
        
        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            var result = other is RapidsNullVariable;

            if (op is RapidsOperator.Inequal)
            {
                result = !result;
            }

            return new RapidsBooleanVariable(result);
        }


        return null;
    }
    
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsNullVariable();
    }
}