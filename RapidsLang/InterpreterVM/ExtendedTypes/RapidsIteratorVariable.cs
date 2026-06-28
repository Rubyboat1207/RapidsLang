using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM.ExtendedTypes;

public class RapidsIteratorVariable(List<(RapidsVariable, RapidsVariable)> iterable) : RapidsVariable
{
    public List<(RapidsVariable, RapidsVariable)> Iterable { get; init; } = iterable;
    public int Index { get; private set; } = 0;
    
    public override string VariableTypeName { get; }
    public override bool Truthy => true;

    public override RapidsVariable? GetMember(string memberName)
    {
        return new RapidsNullVariable();
    }

    public override RapidsVariable ShallowCopy() => new RapidsIteratorVariable(Iterable);

    public override List<(RapidsVariable, RapidsVariable)>? GetIterable() => Iterable;

    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable? other) => new RapidsNullVariable();

    public void Next()
    {
        Index += 1;
    }

    public RapidsVariable GetKey()
    {
        return Iterable[Index].Item1;
    }
    
    public RapidsVariable GetValue()
    {
        return Iterable[Index].Item2;
    }
}