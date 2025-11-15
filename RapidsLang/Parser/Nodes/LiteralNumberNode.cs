using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record LiteralNumberNode(Token BaseToken, double Number) : ExpressionNode(BaseToken)
{
    public override int EndIndex => BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}