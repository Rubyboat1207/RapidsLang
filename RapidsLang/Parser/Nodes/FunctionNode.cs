using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionNode(
    Token OpenTriangle,
    List<ArgumentNode>? Arguments,
    StatementsNode Body,
    StatementsNode? DebugBody=null
) : ExpressionNode(OpenTriangle);