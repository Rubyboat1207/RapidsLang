using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record NullExpression(Token BaseToken) : ExpressionNode(BaseToken)
{
    public override int EndIndex => BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}