namespace RapidsLang.Parser.Nodes;

public record WhileLoopNode(
    ExpressionNode Condition,
    StatementsNode Block,
    int DebugLevel
) : StatementNode(DebugLevel);