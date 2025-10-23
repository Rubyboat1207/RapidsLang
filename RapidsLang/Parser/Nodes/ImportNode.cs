using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ImportNode(
    Token BaseToken,
    Token? AsName
) : Node(BaseToken);