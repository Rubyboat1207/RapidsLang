using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record LiteralNumberNode(Token Token, double Number) : ExpressionNode(Token);