using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ObjectTypeNode(
    Dictionary<Token, TypeNode?> Objects,
    bool Optional
) : TypeNode(Optional);