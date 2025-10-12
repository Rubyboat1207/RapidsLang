using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record AssignmentNode(
    MemberAccessNode Variable,
    Token Operator,
    ExpressionNode Expression,
    int DebugLevel
) : StatementNode(Variable.BaseToken, DebugLevel)
{
    
}