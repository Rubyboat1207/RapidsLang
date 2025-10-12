using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ArgumentNode(
    Token Name,
    TypeNode? Type
) : Node(Name);