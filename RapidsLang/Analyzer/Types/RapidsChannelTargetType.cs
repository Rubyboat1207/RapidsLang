namespace RapidsLang.Analyzer.Types;

public class RapidsChannelTargetType(RapidsType valueType) : RapidsType
{
    private RapidsType ValueType { get; } = valueType;
    public override string Name => $"{ValueType.Name}-^";

    public override Dictionary<string, RapidsType> GetMembers() => new()
    {
        { "writable", RapidsPrimitiveType.Bool },
        { "send", new RapidsFunctionType([new("data", ValueType)], null) }
    };
}