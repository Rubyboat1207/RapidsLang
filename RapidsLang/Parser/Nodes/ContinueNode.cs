using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ContinueNode(Token BaseToken, int DebugLevel) : StatementNode(BaseToken, DebugLevel);