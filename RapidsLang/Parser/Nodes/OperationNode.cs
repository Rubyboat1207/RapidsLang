using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record OperationNode(
    ExpressionNode Left,
    Token Operator,
    ExpressionNode Right
) : ExpressionNode(Left.BaseToken)
{
    public override int EndIndex => Right.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Left, Right];
}