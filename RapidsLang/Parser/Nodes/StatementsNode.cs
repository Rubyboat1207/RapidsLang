using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record StatementsNode : Node
{
    public List<StatementNode> Statements { get; init; }

    public StatementsNode(Token BaseToken, List<StatementNode>? Statements=null) : base(BaseToken)
    {
        this.Statements = Statements ?? [];
    }
}