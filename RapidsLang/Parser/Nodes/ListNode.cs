using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ListNode(
    Token OpenSquare,
    List<ExpressionNode> Values
) : ExpressionNode(OpenSquare);