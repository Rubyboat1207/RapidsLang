using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DeclarationNode(
    Token BaseToken,
    bool Constant,
    Token Name,
    TypeNode? Type,
    ExpressionNode Expression,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);