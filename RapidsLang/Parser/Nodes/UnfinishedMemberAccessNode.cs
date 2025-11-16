using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record UnfinishedMemberAccessNode(
    ExpressionNode Left,
    Token Dot
) : StatementNode(Left.BaseToken, 0)
{
    public override int EndIndex => Dot.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Left];
}