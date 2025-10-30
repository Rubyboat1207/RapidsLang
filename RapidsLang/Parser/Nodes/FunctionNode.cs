using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionNode(
    Token BaseToken,
    List<ArgumentNode>? Arguments,
    StatementsNode Body,
    StatementsNode? DebugBody=null,
    TypeNode? ReturnType=null
) : ExpressionNode(BaseToken);