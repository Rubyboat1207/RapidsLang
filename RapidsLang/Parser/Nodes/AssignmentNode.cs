using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record AssignmentNode(
    MemberAccessNode Variable,
    Token Operator,
    Token? Assignment,
    ExpressionNode Expression,
    int DebugLevel
) : StatementNode(Variable.BaseToken, DebugLevel)
{
    public override int EndIndex => Expression.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Variable, Expression];
}