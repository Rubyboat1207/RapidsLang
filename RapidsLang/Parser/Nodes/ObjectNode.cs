using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ObjectNode(
    Token OpenCurlyBrace,
    List<Tuple<StringNode, ExpressionNode>> keyValues
) : ExpressionNode(OpenCurlyBrace);