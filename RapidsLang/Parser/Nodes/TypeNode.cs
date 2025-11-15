using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public abstract record TypeNode(Token BaseToken, bool Optional) : Node(BaseToken);

public record IdentifierTypeNode(
    Token Identifier,
    bool Optional
) : TypeNode(Identifier, Optional)
{
    public override int EndIndex => Identifier.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}

public record ObjectPropertyTypeNode(
    Token Name,
    TypeNode? Type
) : Node(Name)
{
    public override int EndIndex => Type?.EndIndex ?? Name.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        if (Type is null)
        {
            return [];
        }

        return [Type];
    }
}

public record ObjectTypeNode(
    Token OpenCurly,
    List<ObjectPropertyTypeNode> Properties,
    Token CloseCurly,
    bool Optional
) : TypeNode(OpenCurly, Optional)
{
    public override int EndIndex => CloseCurly.EndIndex;
    public override IEnumerable<Node> GetChildren() => Properties;
}