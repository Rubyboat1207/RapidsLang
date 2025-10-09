using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetStatement(
    Token Name,
    TypeNode? Type
);