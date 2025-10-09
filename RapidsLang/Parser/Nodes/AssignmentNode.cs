using System.Linq.Expressions;
using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record AssignmentNode(
    Expression Variable,
    Token Operator,
    Expression Expression,
    int DebugLevel
) : StatementNode(DebugLevel)
{
    
}