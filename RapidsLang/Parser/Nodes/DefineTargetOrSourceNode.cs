using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetOrSourceNode(
    Token BaseToken,
    Token Name,
    bool IsTarget,
    TypeNode? Type
) : Node(BaseToken);