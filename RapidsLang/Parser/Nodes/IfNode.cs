using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record IfNode(
    Token BaseToken,
    ExpressionNode Condition,
    StatementsNode Block,
    List<ElseNode> ElseNodes,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => ElseNodes.LastOrDefault()?.EndIndex ?? Block.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        var list = new List<Node>([Condition, Block]);
        
        list.AddRange(ElseNodes);

        return list;
    }
}