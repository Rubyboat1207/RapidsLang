namespace RapidsLang.Interpreter.Variables;

public class RapidsNumberVariable(double value) : RapidsVariable
{

    public override string VariableTypeName => "number";
    public override bool Truthy => Value != 0;
    public override RapidsVariable? GetMember(string memberName)
    {
        return null;
    }

    public double Value { get; init; } = value;
    
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable? other)
    {
        if (op is RapidsOperator.Negate)
        {
            return new RapidsNumberVariable(-Value);
        }
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
    
    public override RapidsVariable ShallowCopy()
    {
        return new RapidsNumberVariable(Value);
    }
    
    public override List<(RapidsVariable, RapidsVariable)>? GetIterable() => null;
}