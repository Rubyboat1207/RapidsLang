using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ListNode(
    Token BaseToken,
    List<ExpressionNode> Values
) : ExpressionNode(BaseToken);