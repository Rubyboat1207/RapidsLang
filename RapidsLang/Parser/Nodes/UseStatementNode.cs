using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record UseStatementNode(
    Token Use,
    string ModuleIdentifier,
    int DebugLevel
) : StatementNode(Use, DebugLevel);