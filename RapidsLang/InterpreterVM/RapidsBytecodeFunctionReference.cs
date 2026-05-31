using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM;

public class RapidsBytecodeFunctionReference(int index) : RapidsVariable
{
    public int Index { get; }
    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable? other)
    {
        throw new NotImplementedException();
    }

    public override string VariableTypeName => "function";
    public override bool Truthy => true;
    public override RapidsVariable? GetMember(string memberName)
    {
        throw new NotImplementedException();
    }

    public override RapidsVariable ShallowCopy()
    {
        throw new NotImplementedException();
    }

    public override List<(RapidsVariable, RapidsVariable)>? GetIterable()
    {
        throw new NotImplementedException();
    }
}