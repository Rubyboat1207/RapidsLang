using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record IfNode(
    Token BaseToken,
    ExpressionNode Condition,
    StatementsNode Block,
    List<ElseNode> ElseNodes,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);