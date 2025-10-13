namespace RapidsLang.Interpreter.Variables;

public class RapidsListVariable : RapidsVariable
{
    public override string VariableTypeName => "array";
    public override bool Truthy => List.Count > 0;
    private readonly RapidsFunction _addFunction;
    private readonly RapidsFunction _insertFunction;
    private readonly RapidsFunction _removeAtFunction;
    private readonly RapidsFunction _popFunction;

    public RapidsListVariable(List<RapidsVariable>? list=null)
    {
        List = list ?? [];
        _addFunction = new RapidsNativeFunction(Add);
        _insertFunction = new RapidsNativeFunction(Insert);
        _removeAtFunction = new RapidsNativeFunction(RemoveAt);
        _popFunction = new RapidsNativeFunction(Pop);
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

        if (memberName == "insert")
        {
            return new RapidsFunctionReferenceVariable(_insertFunction);
        }

        if (memberName == "removeAt")
        {
            return new RapidsFunctionReferenceVariable(_removeAtFunction);
        }

        if (memberName == "pop")
        {
            return new RapidsFunctionReferenceVariable(_popFunction);
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

    public void Insert(InterpreterContext ctx)
    {
        if (!ctx.FunctionCallStack.TryPop(out var value))
        {
            // todo: exceptions
            // return RapidsFunctionResult.Err("Expected 1 argument, found 0.");
            return;
        }
        if (!ctx.FunctionCallStack.TryPop(out var indexVar) || indexVar is not RapidsNumberVariable index)
        {
            // todo: exceptions
            // return RapidsFunctionResult.Err("Expected 1 argument, found 0.");
            return;
        }
        
        
        List.Insert((int) index.Value, value);
    }

    public void RemoveAt(InterpreterContext ctx)
    {
        if (!ctx.FunctionCallStack.TryPop(out var indexVar) || indexVar is not RapidsNumberVariable index)
        {
            // todo: exceptions
            // return RapidsFunctionResult.Err("Expected 1 argument, found 0.");
            return;
        }
        
        List.RemoveAt((int) index.Value);
    }

    public void Pop(InterpreterContext ctx)
    {
        if (List.Count == 0)
        {
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        ctx.FunctionCallStack.Push(List.Last());
        List.RemoveAt(List.Count - 1);
    }
}







