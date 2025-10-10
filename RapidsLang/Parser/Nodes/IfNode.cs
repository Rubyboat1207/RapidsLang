namespace RapidsLang.Parser.Nodes;

public record IfNode(
    ExpressionNode Condition,
    StatementsNode Block,
    int DebugLevel
) : StatementNode(DebugLevel);