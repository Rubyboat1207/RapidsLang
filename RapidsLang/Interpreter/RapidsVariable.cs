namespace RapidsLang.Interpreter;

public abstract class RapidsVariable
{
    public abstract RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other);
    public abstract string VariableTypeName { get; }
    public abstract bool Truthy { get; }
    public abstract RapidsVariable? GetMember(string memberName);
}

public class RapidsStringVariable(string value) : RapidsVariable
{
    public string Value { get; init; } = value;
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (op is RapidsOperator.Add)
        {
            return new RapidsStringVariable(Value + Utils.StringifyVariable(other));
        }

        if (op is RapidsOperator.Multiply && other is RapidsNumberVariable num)
        {
            return new RapidsStringVariable(string.Concat(Enumerable.Repeat(Value, (int)num.Value)));
        }

        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            var result = false;
            if (other is RapidsStringVariable oStr)
            {
                result = oStr.Value == Value;
            }

            if (op is RapidsOperator.Inequal)
            {
                result = !result;
            }

            return new RapidsBooleanVariable(result);
        }

        return null;
    }

    public override string VariableTypeName => "string";

    public override bool Truthy => !string.IsNullOrEmpty(Value);
    public override RapidsVariable? GetMember(string memberName)
    {
        if (memberName == "length")
        {
            return new RapidsNumberVariable(memberName.Length);
        }

        return null;
    }
}

public class RapidsNumberVariable(double value) : RapidsVariable
{

    public override string VariableTypeName => "number";
    public override bool Truthy => Value != 0;
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

    public double Value { get; init; } = value;
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsNumberVariable oNum)
        {
            return op switch
            {
                RapidsOperator.Add => new RapidsNumberVariable(Value + oNum.Value),
                RapidsOperator.Subtract => new RapidsNumberVariable(Value - oNum.Value),
                RapidsOperator.Divide => new RapidsNumberVariable(Value / oNum.Value),
                RapidsOperator.Multiply => new RapidsNumberVariable(Value * oNum.Value),
                RapidsOperator.Modulo => new RapidsNumberVariable(Value % oNum.Value),
                RapidsOperator.LessThan => new RapidsBooleanVariable(Value < oNum.Value),
                RapidsOperator.GreaterThan => new RapidsBooleanVariable(Value > oNum.Value),
                RapidsOperator.LessThanEqualTo => new RapidsBooleanVariable(Value <= oNum.Value),
                RapidsOperator.GreaterThanEqualTo => new RapidsBooleanVariable(Value >= oNum.Value),
                RapidsOperator.Equality => new RapidsBooleanVariable(Math.Abs(Value - oNum.Value) < 0.000001),
                RapidsOperator.Inequal => new RapidsBooleanVariable(Math.Abs(Value - oNum.Value) > 0.000001),
                _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
            };
        }
        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            return new RapidsBooleanVariable(false);
        }

        if (other is RapidsStringVariable rString && op == RapidsOperator.Add)
        {
            return new RapidsStringVariable(Value + rString.Value);
        }

        return null;
    }
}

public class RapidsObjectVariable : RapidsVariable
{
    public override string VariableTypeName => "object";
    public override bool Truthy => ObjectValues.Count > 0;
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

    public Dictionary<string, RapidsVariable> ObjectValues { get; } = [];
    
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
}

public class RapidsListVariable : RapidsVariable
{
    public override string VariableTypeName => "array";
    public override bool Truthy => List.Count > 0;
    private readonly RapidsFunction _addFunction;

    public RapidsListVariable(List<RapidsVariable>? list=null)
    {
        List = list ?? [];
        _addFunction = new RapidsNativeFunction(Add);
    }

    public override RapidsVariable? GetMember(string memberName)
    {
        if (memberName == "add")
        {
            return new RapidsFunctionReferenceVariable(_addFunction);
        }

        return null;
    }

    public List<RapidsVariable> List { get; private init; }
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (other is RapidsStringVariable rString)
        {
            return new RapidsStringVariable(Utils.StringifyVariable(this) + rString.Value);
        }
        
        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            var result = other is RapidsListVariable oList && oList.List == List;

            if (op is RapidsOperator.Inequal)
            {
                result = !result;
            }

            return new RapidsBooleanVariable(result);
        }

        if (op is RapidsOperator.Index && other is RapidsNumberVariable oNum)
        {
            return List[(int)oNum.Value];
        }

        return null;
    }

    public void Add(InterpreterContext ctx)
    {
        if (!ctx.FunctionCallStack.TryPop(out var result))
        {
            // todo: exceptions
            // return RapidsFunctionResult.Err("Expected 1 argument, found 0.");
        }
        List.Add(result!);
    }
}

public class RapidsFunctionReferenceVariable(RapidsFunction function) : RapidsVariable
{
    public override string VariableTypeName => "function";
    public override bool Truthy => true;
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

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
}

public class RapidsBooleanVariable(bool value): RapidsVariable
{
    public override string VariableTypeName => "bool";
    public override bool Truthy => Value;
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

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

        return null;
    }
}

public class RapidsNullVariable : RapidsVariable
{
    public override string VariableTypeName => "null";
    public override bool Truthy => false;
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

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
}