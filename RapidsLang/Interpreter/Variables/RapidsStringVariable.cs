namespace RapidsLang.Interpreter.Variables;

public class RapidsStringVariable : RapidsVariable
{
    private readonly RapidsFunction _substrFunction;

    public string Value { get; init; }
    
    public RapidsStringVariable(string value)
    {
        Value = value;
        _substrFunction = new RapidsNativeFunction(SubStr);
    }
    
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

        if (op is RapidsOperator.Index && other is RapidsNumberVariable number)
        {
            return new RapidsStringVariable(Value[(int)number.Value].ToString());
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

        if (memberName == "substr")
        {
            return new RapidsFunctionReferenceVariable(_substrFunction);
        }

        return null;
    }

    public void SubStr(InterpreterContext ctx)
    {
        if (!ctx.FunctionCallStack.TryPop(out var index) || index is not RapidsNumberVariable start)
        {
            // todo error
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        
        if (!ctx.FunctionCallStack.TryPop(out var endIndex) || endIndex is not RapidsNumberVariable end)
        {
            // only start
            ctx.FunctionCallStack.Push(new RapidsStringVariable(Value[(int) start.Value..]));

            return;
        }
        
        ctx.FunctionCallStack.Push(new RapidsStringVariable(Value[(int) start.Value..(int) end.Value]));
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsStringVariable(Value);
    }
}