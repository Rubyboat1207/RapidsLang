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
        return memberName switch
        {
            "length" => new RapidsNumberVariable(Value.Length),
            "substr" => new RapidsFunctionReferenceVariable(_substrFunction),
            "split" => RapidsFunctionReferenceVariable.OfNative(Split),
            "contains" => RapidsFunctionReferenceVariable.OfNative(Contains),
            "lastIndexOf" => RapidsFunctionReferenceVariable.OfNative(LastIndexOf),
            "trim" => RapidsFunctionReferenceVariable.OfNative(Trim),
            _ => null
        };
    }

    public void SubStr(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        if (!ctx.FunctionCallStack.TryPop(out var index) || index is not RapidsNumberVariable end)
        {
            // todo error
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        
        if (!ctx.FunctionCallStack.TryPop(out var endIndex) || endIndex is not RapidsNumberVariable start)
        {
            ctx.FunctionCallStack.Push(new RapidsNullVariable());

            return;
        }
        
        ctx.FunctionCallStack.Push(new RapidsStringVariable(Value[(int) start.Value..(int) end.Value]));
    }

    public void Split(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil().GuaranteeReturn();

        var splitter = util.LatestString();

        if (splitter is null)
        {
            return;
        }

        util.Return(new RapidsListVariable(Value.Split(splitter.Value).Select(s => (RapidsVariable) new RapidsStringVariable(s)).ToList()));
    }

    public void Contains(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil().GuaranteeReturn();

        var substring = util.LatestString();

        if (substring is null)
        {
            return;
        }
        
        util.Return(new RapidsBooleanVariable(Value.Contains(substring.Value)));
    }

    public void LastIndexOf(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil().GuaranteeReturn();

        var substring = util.LatestString();

        if (substring is null)
        {
            return;
        }
        
        util.Return(new RapidsNumberVariable(Value.LastIndexOf(substring.Value, StringComparison.Ordinal)));
    }

    public void Trim(RapidsInterpreter interpreter)
    {
        interpreter.Context.FunctionCallStack.Push(new RapidsStringVariable(Value.Trim()));
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsStringVariable(Value);
    }
}