namespace RapidsLang.Analyzer.Types;

public class RapidsStringType : RapidsType
{
    public override string Name => "string";

    public override Dictionary<string, RapidsType> GetMembers() => new()
    {
        { "length", RapidsPrimitiveType.Number },
        {
            "substr", new RapidsFunctionType(
                [
                    new("from", RapidsPrimitiveType.Number),
                    new("to", RapidsPrimitiveType.Number)
                ],
                this)
        },
        {
            "split", new RapidsFunctionType(
                [
                    new("splitter", this)
                ],
                new RapidsArrayType(this)
            )
        }
    };

    public static RapidsStringType Instance = new();

    public override RapidsType? IndexType => Instance;
}