namespace RapidsLang.Parser.Nodes;

public record ListNode(
    List<ExpressionNode> Values
) : ExpressionNode;