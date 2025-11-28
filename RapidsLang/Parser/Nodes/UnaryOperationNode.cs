using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;


public record UnaryOperationNode(Token Operation, ExpressionNode ExpressionNode) : ExpressionNode(Operation)
{
    public override int EndIndex => ExpressionNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [ExpressionNode];
}