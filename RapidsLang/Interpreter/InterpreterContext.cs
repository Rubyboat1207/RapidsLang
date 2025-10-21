using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public class InterpreterContext
{
    public Stack<RapidsVariable> FunctionCallStack = [];
    public Dictionary<string, VariableHolder> variables { get; init; } = [];
    public ModuleRegistry ModuleRegistry = new();

    public void AddNativeFunction(string name, Action<InterpreterContext> func)
    {
        variables.Add(
            name,
            new(
                new RapidsFunctionReferenceVariable(new RapidsNativeFunction(func)),
                true
            )
        );
    }
}

public class VariableHolder(RapidsVariable Variable, bool Constant, TypeNode? TypeNode = null)
{
    public virtual RapidsVariable Variable { get; set; } = Variable;
    public bool Constant { get; } = Constant;
    public TypeNode? TypeNode { get; } = TypeNode;
}