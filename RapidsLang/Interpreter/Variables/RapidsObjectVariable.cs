namespace RapidsLang.Interpreter.Variables;

public class RapidsObjectVariable(Dictionary<string, RapidsVariable>? initialValues) : RapidsVariable
{
    public override string VariableTypeName => "object";
    public override bool Truthy => ObjectValues.Count > 0;
    public Dictionary<string, RapidsVariable> ObjectValues { get; } = initialValues ?? [];
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsStringVariable rString)
        {
            return new RapidsStringVariable(Utils.StringifyVariable(this) + rString.Value);
        }
        
        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            var result = other == this;

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
        if (ObjectValues.ContainsKey(memberName))
        {
            return ObjectValues[memberName];
        }

        if (memberName == "__keys__")
        {
            return new RapidsListVariable(ObjectValues.Values.ToList());
        }

        return null;
    }
    
    public override RapidsVariable ShallowCopy()
    {
        return new RapidsObjectVariable(ObjectValues);
    }
}