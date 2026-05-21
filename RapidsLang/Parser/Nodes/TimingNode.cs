using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record TimingNode(Token BaseToken, ExpressionNode Time) : Node(BaseToken)
{
    public override int EndIndex => Time.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Time];
}