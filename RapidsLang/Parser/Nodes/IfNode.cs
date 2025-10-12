using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record IfNode(
    Token If,
    ExpressionNode Condition,
    StatementsNode Block,
    int DebugLevel
) : StatementNode(If, DebugLevel);