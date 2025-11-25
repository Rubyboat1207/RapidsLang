using RapidsLang.Lexer;
using RapidsLang.Parser.Types;

namespace RapidsLang.Parser.Nodes;

public abstract record TypeNode(Token BaseToken, bool Optional) : Node(BaseToken);


// eg: number, string, bool
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
    TypeNode InnerType,
    Token? OpenTriangle,
    IdentifierNode? DataName,
    Token? ClosedTriangle
) : TypeNode(Minus, InnerType.Optional)
{
    public override int EndIndex => ClosedTriangle?.EndIndex ?? InnerType.EndIndex;
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

public record FunctionParamTypeNode(
    IdentifierNode Name,
    TypeNode Type
) : Node(Name.BaseToken)
{
    public override int EndIndex => Type.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Name, Type];
}

public record FunctionTypeNode(
    Token OpenParen,
    List<FunctionParamTypeNode> parameters,
    Token ClosedParen,
    Token OpenTriangle,
    TypeNode ReturnType,
    bool Optional
) : TypeNode(OpenParen, Optional)
{
    // should be the question mark if optional but whatever
    public override int EndIndex => ReturnType.EndIndex;
    public override IEnumerable<Node> GetChildren() => [..parameters, ReturnType];
}

public record UnionTypeNode(
    TypeNode A,
    Token Ampersand,
    TypeNode B,
    bool Optional
) : TypeNode(A.BaseToken, Optional)
{
    public override int EndIndex => B.EndIndex;
    public override IEnumerable<Node> GetChildren() => [A, B];
}

public record ParenthesizedTypeNode(
    Token OpenParen,
    TypeNode InnerType,
    Token CloseParen,
    bool Optional
) : TypeNode(OpenParen, Optional)
{
    public override int EndIndex => Optional ? CloseParen.EndIndex + 1 : CloseParen.EndIndex; // +1 for the '?'
    public override IEnumerable<Node> GetChildren() => [InnerType];
}