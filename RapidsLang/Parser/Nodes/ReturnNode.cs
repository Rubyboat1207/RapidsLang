using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ReturnNode(
    Token Ret,
    ExpressionNode Value,
    int DebugLevel
) : StatementNode(Ret, DebugLevel);