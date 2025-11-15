using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record MemberAccessNode(
    ExpressionNode? Left,
    Token MemberName
) : ExpressionNode(MemberName)
{
    public override int EndIndex => MemberName.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        if (Left is not null)
        {
            return [Left];
        }

        return [];
    }
}