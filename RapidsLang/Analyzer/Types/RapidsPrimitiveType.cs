namespace RapidsLang.Analyzer.Types;

public class RapidsPrimitiveType(string name, Func<RapidsPrimitiveType, Dictionary<string, RapidsType>?>? membersCallback=null) : RapidsType
{
    public override string Name { get; } = name;
    private Dictionary<string, RapidsType>? Members => membersCallback is null ? [] : membersCallback(this);
    public override Dictionary<string, RapidsType> GetMembers() => Members ?? [];

    // Singleton instances for common primitives
    public static readonly RapidsPrimitiveType Number = new("number");
    public static readonly RapidsPrimitiveType Bool = new("boolean");
    public static readonly RapidsPrimitiveType Null = new("null");
}