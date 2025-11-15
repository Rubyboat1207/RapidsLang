namespace RapidsLang.Parser.Nodes;

public record FunctionCallStatementNode(
    FunctionCallExpressionNode Function,
    int DebugLevel
) : StatementNode(Function.BaseToken, DebugLevel)
{
    public override int EndIndex => Function.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Function];
}