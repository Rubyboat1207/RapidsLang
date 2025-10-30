using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public abstract record TypeNode(Token BaseToken, bool Optional) : Node(BaseToken);

public record IdentifierTypeNode(
    Token Identifier,
    bool Optional
) : TypeNode(Identifier, Optional);

public record ObjectPropertyTypeNode(
    Token Name,
    TypeNode? Type
) : Node(Name);

public record ObjectTypeNode(
    Token OpenCurly,
    List<ObjectPropertyTypeNode> Properties,
    Token CloseCurly,
    bool Optional
) : TypeNode(OpenCurly, Optional);