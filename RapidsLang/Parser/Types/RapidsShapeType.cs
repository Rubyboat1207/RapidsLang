namespace RapidsLang.Parser.Types;

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
        return Properties.GetValueOrDefault(memberName);
    }
}