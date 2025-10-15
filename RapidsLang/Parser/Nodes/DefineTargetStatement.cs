using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetStatement(
    Token BaseToken,
    Token Name,
    TypeNode? Type,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);