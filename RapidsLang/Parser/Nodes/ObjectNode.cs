using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ObjectNode(
    Token BaseToken,
    List<Tuple<StringNode, ExpressionNode>> KeyValues
) : ExpressionNode(BaseToken)
{
    public override int EndIndex => KeyValues.LastOrDefault()?.Item2.EndIndex ?? BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => KeyValues.Select(expr => expr.Item2);
}