using System.Linq.Expressions;
using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DeclarationNode(
    bool Constant,
    Token Name,
    TypeNode? Type,
    Expression Expression,
    int DebugLevel
) : StatementNode(DebugLevel);