using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public class InterpreterContext
{
    public Stack<RapidsVariable> FunctionCallStack = [];
    public Dictionary<string, VariableHolder> variables { get; init; } = [];

    public void AddExternalFunction(string name, Func<InterpreterContext, RapidsFunctionResult> func)
    {
        variables.Add(
            name,
            new(
                new RapidsFunctionReferenceVariable(new(func)),
                true
            )
        );
    }
}

public class VariableHolder(RapidsVariable Variable, bool Constant, TypeNode? TypeNode = null)
{
    public RapidsVariable Variable { get; set; } = Variable;
    public bool Constant { get; } = Constant;
    public TypeNode? TypeNode { get; } = TypeNode;
}