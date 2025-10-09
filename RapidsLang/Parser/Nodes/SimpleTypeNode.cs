using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record SimpleTypeNode(
    Token Name,
    bool Optional
) : TypeNode(Optional);