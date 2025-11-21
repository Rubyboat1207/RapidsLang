using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ReturnNode(
    Token BaseToken,
    ExpressionNode? Value,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => Value?.EndIndex ?? BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => Value is null ? [] : [Value];
}