using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record PipeStatement(
    Token Pipe,
    ExpressionNode Source,
    ExpressionNode FormatExpression,
    ExpressionNode Target,
    int DebugLevel
) : StatementNode(DebugLevel);