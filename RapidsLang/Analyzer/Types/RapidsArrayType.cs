using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Analyzer.Types;

public class RapidsArrayType(RapidsType elementType) : RapidsType
{
    public RapidsType ElementType { get; } = elementType;
    public override string Name => $"{ElementType.Name}[]";

    public override Dictionary<string, RapidsType> GetMembers() => new()
    {
        { "add", RapidsListVariable.AddType },
        { "length", RapidsPrimitiveType.Number },
        { "insert", RapidsListVariable.InsertType },
        { "removeAt", RapidsListVariable.RemoveAtType },
        { "pop", RapidsListVariable.PopType },
    };

    public override RapidsType? IndexType => ElementType;
}