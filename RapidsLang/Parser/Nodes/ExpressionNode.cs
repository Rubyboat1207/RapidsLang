using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public abstract record ExpressionNode(Token BaseToken) : Node(BaseToken);