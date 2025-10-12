namespace RapidsLang.Interpreter.Variables;

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

        if (memberName == "length")
        {
            return new RapidsNumberVariable(List.Count);
        }

        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsListVariable(List);
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