namespace RapidsLang.Analyzer.Types;

public class RapidsDictionaryType(RapidsType valueType) : RapidsType
{
    private RapidsType ValueType { get; } = valueType;

    public override string Name => $"{ValueType.Name}{{}}";
    public override Dictionary<string, RapidsType> GetMembers() => [];

    public override RapidsType? GetMember(string memberName)
    {
        return new RapidsUnionType([ValueType, RapidsPrimitiveType.Null]);
    }

    public static readonly RapidsDictionaryType Unparameterized = new(RapidsAnyType.Instance);

    public override RapidsType? IndexType => RapidsAnyType.Instance;
}