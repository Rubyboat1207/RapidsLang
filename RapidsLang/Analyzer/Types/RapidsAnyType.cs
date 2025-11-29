namespace RapidsLang.Analyzer.Types;

public class RapidsAnyType : RapidsType
{
    public override string Name => "any";
    public override Dictionary<string, RapidsType> GetMembers() => [];
    public override bool IsSameType(RapidsType other) => true;

    public static RapidsAnyType Instance { get; } = new();
    public override RapidsType? IterableType => Instance;
}