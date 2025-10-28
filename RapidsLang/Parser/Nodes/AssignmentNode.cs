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
    
}