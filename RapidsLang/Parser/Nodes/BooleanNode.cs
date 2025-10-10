using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record BooleanNode(
    Token value
) : ExpressionNode;