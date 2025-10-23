using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record UseStatementNode(
    Token BaseToken,
    string ModuleIdentifier,
    List<ImportNode>? ImportNodes,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);