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

public record ChannelSourceTypeNode(
    Token Minus, 
    Token Caret, 
    TypeNode InnerType
) : TypeNode(Minus, InnerType.Optional) // Optionality usually bubbles up or is handled by parent
{
    public override int EndIndex => InnerType.EndIndex;
    public override IEnumerable<Node> GetChildren() => [InnerType];
}

public record ChannelTargetTypeNode(
    TypeNode InnerType, 
    Token Minus, 
    Token Caret
) : TypeNode(InnerType.BaseToken, InnerType.Optional)
{
    public override int EndIndex => Caret.EndIndex;
    public override IEnumerable<Node> GetChildren() => [InnerType];
}

public record BiDirectionalChannelTypeNode(
    Token OpenParen,
    TypeNode Left,
    Token Ampersand,
    TypeNode Right,
    Token CloseParen
) : TypeNode(OpenParen, false)
{
    public override int EndIndex => CloseParen.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Left, Right];
}