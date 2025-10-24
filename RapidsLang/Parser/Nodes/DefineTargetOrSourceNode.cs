using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetOrSourceNode(
    Token BaseToken,
    Token Name,
    bool IsTarget,
    Token? DataName,
    TypeNode? Type
) : Node(BaseToken);