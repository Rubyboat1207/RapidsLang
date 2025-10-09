using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionDeclarationNode(
    Token Name,
    List<ArgumentNode>? Arguments,
    StatementsNode Body
) : ExpressionNode;