using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record IdentifierNode(
    Token Token
) : ExpressionNode;