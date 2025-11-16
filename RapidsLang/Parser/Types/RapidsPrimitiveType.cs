namespace RapidsLang.Parser.Types;

public class RapidsPrimitiveType(string name, Dictionary<string, RapidsType>? members=null) : RapidsType
{
    public override string Name { get; } = name;
    public Dictionary<string, RapidsType>? Members { get; } = members;
    public override Dictionary<string, RapidsType> GetMembers() => Members ?? [];

    // Singleton instances for common primitives
    public static readonly RapidsPrimitiveType Number = new("number");
    public static readonly RapidsPrimitiveType String = new("string");
    public static readonly RapidsPrimitiveType Bool = new("boolean");
    public static readonly RapidsPrimitiveType Null = new("null");
}