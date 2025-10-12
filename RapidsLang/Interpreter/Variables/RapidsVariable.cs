namespace RapidsLang.Interpreter.Variables;

public abstract class RapidsVariable
{
    public abstract RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other);
    public abstract string VariableTypeName { get; }
    public abstract bool Truthy { get; }
    public abstract RapidsVariable? GetMember(string memberName);
    public abstract RapidsVariable ShallowCopy();
}