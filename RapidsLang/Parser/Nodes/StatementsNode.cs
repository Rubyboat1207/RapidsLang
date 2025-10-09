namespace RapidsLang.Parser.Nodes;

public record StatementsNode : Node
{
    public List<StatementNode> Statements { get; init; }

    public StatementsNode(List<StatementNode>? Statements=null)
    {
        this.Statements = Statements ?? [];
    }
}