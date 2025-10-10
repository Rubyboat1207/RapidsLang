using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionNode(
    List<ArgumentNode>? Arguments,
    StatementsNode Body
) : ExpressionNode;