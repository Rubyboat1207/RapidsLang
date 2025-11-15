using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record PipeStatement(
    Token BaseToken,
    ExpressionNode Source,
    ExpressionNode FormatExpression,
    ExpressionNode Target,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => Target.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Source, FormatExpression, Target];
}