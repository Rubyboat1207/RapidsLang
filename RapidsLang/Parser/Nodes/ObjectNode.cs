using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ObjectNode(
    Token BaseToken,
    List<Tuple<StringNode, ExpressionNode>> keyValues
) : ExpressionNode(BaseToken);