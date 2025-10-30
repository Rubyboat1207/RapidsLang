namespace RapidsLang.Interpreter.Variables;

public class RapidsObjectVariable(Dictionary<string, RapidsVariable>? initialValues=null) : RapidsVariable
{
    public override string VariableTypeName => "object";
    public override bool Truthy => ObjectValues.Count > 0;
    public Dictionary<string, RapidsVariable> ObjectValues { get; } = initialValues ?? [];
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsStringVariable rString)
        {
            if (op is RapidsOperator.Add)
            {
                return new RapidsStringVariable(Utils.StringifyVariable(this) + rString.Value);
            }
            
            
        }
        
        if(op is RapidsOperator.Index)
        {
            if (ObjectValues.TryGetValue(Utils.StringifyVariable(other), out var value))
            {
                return value;
            }
            return new RapidsNullVariable();
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
            return new RapidsListVariable(ObjectValues.Keys.Select(k => (RapidsVariable) new RapidsStringVariable(k)).ToList());
        }else if (memberName == "__values__")
        {
            return new RapidsListVariable(ObjectValues.Values.ToList());
        }else if (memberName == "length")
        {
            return new RapidsNumberVariable(ObjectValues.Count);
        }

        return null;
    }
    
    public override RapidsVariable ShallowCopy()
    {
        return new RapidsObjectVariable(ObjectValues);
    }

    public VariableHolder? GetMemberReference(string memberName)
    {
        if (ObjectValues.ContainsKey(memberName))
        {
            return new ObjectMemberVariableHolder(ObjectValues[memberName], this, memberName);
        }else
        {
            var member = GetMember(memberName);
            if (member is null)
            {
                return new ObjectMemberVariableHolder(new RapidsNullVariable(), this, memberName);
            }
            return new VariableHolder(member, true);
        }
    }
}

public class ObjectMemberVariableHolder(RapidsVariable InitialValue, RapidsObjectVariable Object, string Key) : VariableHolder(InitialValue, false)
{
    public override RapidsVariable Variable
    {
        get => Object.ObjectValues[Key];
        set => Object.ObjectValues[Key] = value;
    }
}