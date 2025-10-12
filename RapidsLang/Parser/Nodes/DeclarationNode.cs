using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DeclarationNode(
    Token Declaration,
    bool Constant,
    Token Name,
    TypeNode? Type,
    ExpressionNode Expression,
    int DebugLevel
) : StatementNode(Declaration, DebugLevel);