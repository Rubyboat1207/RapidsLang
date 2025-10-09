using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record OperationNode(
    ExpressionNode Left,
    Token Operator,
    ExpressionNode Right
) : ExpressionNode;