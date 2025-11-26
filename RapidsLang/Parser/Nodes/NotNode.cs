using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record NotNode(Token Not, ExpressionNode ExpressionNode) : ExpressionNode(Not)
{
    public override int EndIndex => ExpressionNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [ExpressionNode];
}