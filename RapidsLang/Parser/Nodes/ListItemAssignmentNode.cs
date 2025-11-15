using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ListItemAssignmentNode(
    ExpressionNode Array,
    ExpressionNode Index,
    Token Operator,
    ExpressionNode Value,
    int DebugLevel
) : StatementNode(Operator, DebugLevel)
{
    public override int EndIndex => Value.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Array, Index, Value];
}