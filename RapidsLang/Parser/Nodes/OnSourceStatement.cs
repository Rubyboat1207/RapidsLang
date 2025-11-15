using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record OnSourceStatement(
    Token BaseToken,
    ExpressionNode Source,
    StatementsNode Body,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => Body.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Source, Body];
}