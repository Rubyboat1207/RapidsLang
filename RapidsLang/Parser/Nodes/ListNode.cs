using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ListNode(
    Token BaseToken,
    List<ExpressionNode> Values
) : ExpressionNode(BaseToken)
{
    public override int EndIndex => Values.LastOrDefault()?.EndIndex ?? BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => Values;
}