using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record WhileLoopNode(
    Token While,
    ExpressionNode Condition,
    StatementsNode Block,
    int DebugLevel
) : StatementNode(While, DebugLevel);