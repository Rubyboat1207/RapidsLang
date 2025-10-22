using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetOrSourceStatement(
    Token BaseToken,
    DefineTargetOrSourceNode TargetOrSourceNode,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);