using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ReturnNode(
    Token BaseToken,
    ExpressionNode Value,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);