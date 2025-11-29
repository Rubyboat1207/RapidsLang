namespace RapidsLang.Analyzer.Types;

public abstract class RapidsType
{
    public abstract string Name { get; }
    public abstract Dictionary<string, RapidsType> GetMembers();
    public virtual RapidsType? GetMember(string memberName) => GetMembers().GetValueOrDefault(memberName);

    public virtual bool IsSameType(RapidsType other) => other is RapidsAnyType || Name == other.Name;
    public virtual RapidsType? IndexType { get; } = null;
    public virtual RapidsType? IterableType => null;
}