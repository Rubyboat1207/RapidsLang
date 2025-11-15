using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record BreakNode(Token BaseToken, int DebugLevel) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}