using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ElseNode(
    Token BaseToken,
    ExpressionNode? Condition,
    StatementsNode Block
) : Node(BaseToken);