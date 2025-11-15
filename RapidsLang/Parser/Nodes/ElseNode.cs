using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ElseNode(
    Token BaseToken,
    Token? IfToken,
    ExpressionNode? Condition,
    StatementsNode Block
) : Node(BaseToken)
{
    public override int EndIndex => Block.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        if (Condition is null)
        {
            return [Block];
        }

        return [Condition, Block];
    }
}