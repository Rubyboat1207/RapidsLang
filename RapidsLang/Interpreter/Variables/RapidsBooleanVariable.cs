namespace RapidsLang.Interpreter.Variables;

public class RapidsBooleanVariable(bool value): RapidsVariable
{
    public override string VariableTypeName => "bool";
    public override bool Truthy => Value;

    public bool Value { get; init; } = value;
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsStringVariable rString)
        {
            return new RapidsStringVariable(Value + rString.Value);
        }
        
        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            var result = false;
            if (other is RapidsBooleanVariable oBool)
            {
                result = oBool.Value == Value;
            }

            if (op is RapidsOperator.Inequal)
            {
                result = !result;
            }

            return new RapidsBooleanVariable(result);
        }

        if (op is RapidsOperator.AndAnd && other is RapidsBooleanVariable oBool2)
        {
            return new RapidsBooleanVariable(Value && oBool2.Value);
        }
        
        if (op is RapidsOperator.OrOr && other is RapidsBooleanVariable oBool3)
        {
            return new RapidsBooleanVariable(Value || oBool3.Value);
        }

        return null;
    }
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsBooleanVariable(Value);
    }
}