using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record BreakNode(Token Break, int DebugLevel) : StatementNode(Break, DebugLevel);