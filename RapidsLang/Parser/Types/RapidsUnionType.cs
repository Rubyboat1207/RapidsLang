namespace RapidsLang.Parser.Types;

public class RapidsUnionType : RapidsType
{
    public HashSet<RapidsType> Types { get; }

    public RapidsUnionType(HashSet<RapidsType> types)
    {
        Types = new HashSet<RapidsType>();
        foreach (var type in types)
        {
            if (type is RapidsUnionType nestedUnion)
            {
                Types.UnionWith(nestedUnion.Types);
            }
            else
            {
                Types.Add(type);
            }
        }
    }

    public override string Name
    {
        get
        {
            var sortedNames = Types.Select(t => t.Name).OrderBy(name => name);
            return string.Join(" | ", sortedNames);
        }
    }

    // todo: support this
    public override Dictionary<string, RapidsType> GetMembers() => [];

    public override bool IsSameType(RapidsType other)
    {
        if (other is not RapidsUnionType otherUnion)
        {
            return false;
        }

        return Types.SetEquals(otherUnion.Types);
    }
}