using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ElseNode(
    Token BaseToken,
    Token? IfToken,
    ExpressionNode? Condition,
    StatementsNode Block
) : Node(BaseToken);