using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetStatement(
    Token Define,
    Token Name,
    TypeNode? Type,
    int DebugLevel
) : StatementNode(Define, DebugLevel);