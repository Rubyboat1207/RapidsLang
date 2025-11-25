using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record IdentifierNode(
    Token Token
) : ExpressionNode(Token)
{
    public override int EndIndex => Token.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
    public string Value => Token.Value;
}