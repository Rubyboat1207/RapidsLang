namespace RapidsLang.Analyzer.Types;

public class RapidsShapeType(Dictionary<string, RapidsType> properties) : RapidsType
{
    private Dictionary<string, RapidsType> Properties { get; } = properties;

    public override string Name
    {
        get
        {
            var props = Properties.Select(kvp => $"{kvp.Key}: {kvp.Value.Name}");
            return $"{{ {string.Join(", ", props)} }}";
        }
    }

    public override Dictionary<string, RapidsType> GetMembers() => Properties;

    public override RapidsType? GetMember(string memberName)
    {
        if (memberName == "__keys__")
        {
            return new RapidsArrayType(RapidsStringType.Instance);
        }
        return Properties.GetValueOrDefault(memberName);
    }

    public override RapidsType? IndexType => RapidsAnyType.Instance;
    public override RapidsType? IterableType => RapidsAnyType.Instance;
}