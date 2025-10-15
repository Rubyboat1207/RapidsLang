using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record PipeStatement(
    Token BaseToken,
    ExpressionNode Source,
    ExpressionNode FormatExpression,
    ExpressionNode Target,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);