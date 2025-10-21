using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public class InterpreterContext
{
    public Stack<RapidsVariable> FunctionCallStack = [];
    public Dictionary<string, VariableHolder> variables { get; } = [];
    public ModuleRegistry ModuleRegistry = new();
    public ModuleExports Exports = new();
}

public class VariableHolder(RapidsVariable Variable, bool Constant, TypeNode? TypeNode = null)
{
    public virtual RapidsVariable Variable { get; set; } = Variable;
    public bool Constant { get; } = Constant;
    public TypeNode? TypeNode { get; } = TypeNode;
}