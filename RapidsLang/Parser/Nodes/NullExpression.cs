using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record NullExpression(Token BaseToken) : ExpressionNode(BaseToken);