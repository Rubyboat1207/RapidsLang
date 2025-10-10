using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DeclarationNode(
    bool Constant,
    Token Name,
    TypeNode? Type,
    ExpressionNode Expression,
    int DebugLevel
) : StatementNode(DebugLevel);