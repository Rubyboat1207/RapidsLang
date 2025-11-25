namespace RapidsLang.Analyzer.Types;

public class RapidsChannelSourceType(RapidsType valueType, string? callbackVariableName) : RapidsType
{
    public RapidsType ValueType { get; } = valueType;
    public string? CallbackVariableName { get; } = callbackVariableName;
    public override string Name => $"-^{ValueType.Name}";

    public override Dictionary<string, RapidsType> GetMembers() => new()
    {
        { "readable", RapidsPrimitiveType.Bool },
        {
            "on_data", new RapidsFunctionType([
                new("callback", new RapidsFunctionType([new("data", ValueType)], null))
            ], null)
        }
    };
}