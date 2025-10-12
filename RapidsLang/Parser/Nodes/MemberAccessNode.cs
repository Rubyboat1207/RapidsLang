using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record MemberAccessNode(
    ExpressionNode? Left,
    Token MemberName
) : ExpressionNode(MemberName);