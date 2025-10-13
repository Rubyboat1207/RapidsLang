using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ContinueNode(Token Continue, int DebugLevel) : StatementNode(Continue, DebugLevel);