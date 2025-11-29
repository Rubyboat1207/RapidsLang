using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Analyzer.Types;

public class RapidsStringType : RapidsType
{
    public override string Name => "string";

    public override Dictionary<string, RapidsType> GetMembers() => new()
    {
        { "length", RapidsPrimitiveType.Number },
        {
            "substr",
            new RapidsFunctionType(
                [
                    new("from", RapidsPrimitiveType.Number),
                    new("to", RapidsPrimitiveType.Number)
                ],
                this
            )
        },
        {
            "split",
            new RapidsFunctionType(
                [
                    new("splitter", this)
                ],
                new RapidsArrayType(this)
            )
        },
        {
            "contains",
            new RapidsFunctionType(
                [
                    new("substring", this)
                ],
                RapidsPrimitiveType.Bool
            )
        },
        {
            "lastIndexOf",
            new RapidsFunctionType(
                [
                    new("substring", this)
                ],
                RapidsPrimitiveType.Number
            )
        },
        {
            "trim",
            new RapidsFunctionType(
                [],
                this
            )
        }
    };

    public static readonly RapidsStringType Instance = new();

    public override RapidsType? IndexType => Instance;

    public override RapidsType? IterableType => Instance;
}