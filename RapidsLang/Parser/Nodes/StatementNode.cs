using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public abstract record StatementNode(Token BaseToken, int DebugLevel) : Node(BaseToken);