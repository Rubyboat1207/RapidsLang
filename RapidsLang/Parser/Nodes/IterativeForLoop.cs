using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record IterativeForLoop(
    Token For,
    IdentifierNode Item,
    Token? At,
    IdentifierNode? Index,
    Token In,
    ExpressionNode Iterable,
    StatementsNode Body
) : StatementNode(For, 0)
{
    public override int EndIndex => Body.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        List<Node> nodes = [Item, Iterable, Body];

        if (Index is not null)
        {
            nodes.Add(Index);
        }

        return nodes;
    }
}