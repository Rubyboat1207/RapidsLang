using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record MemberAccessNode(
    ExpressionNode? Left,
    IdentifierNode MemberName
) : ExpressionNode(Left?.BaseToken ?? MemberName.BaseToken)
{
    public override int EndIndex => MemberName.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        if (Left is not null)
        {
            return [Left, MemberName];
        }

        return [MemberName];
    }
}