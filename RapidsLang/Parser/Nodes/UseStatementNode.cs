using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record UseStatementNode(
    Token BaseToken,
    string ModuleIdentifier,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);