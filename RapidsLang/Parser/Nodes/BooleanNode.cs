using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record BooleanNode(
    Token Value
) : ExpressionNode(Value);