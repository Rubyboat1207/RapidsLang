namespace RapidsLang.Analyzer.Types;

public class RapidsBiDirectionalChannelType(RapidsChannelSourceType source, RapidsChannelTargetType target) : RapidsType
{
    public RapidsChannelSourceType Source { get; } = source;
    public RapidsChannelTargetType Target { get; } = target;
    public override string Name => $"({Source.Name}&{Target.Name})";

    public override Dictionary<string, RapidsType> GetMembers() => new([.. Source.GetMembers(), .. Target.GetMembers()]);
}