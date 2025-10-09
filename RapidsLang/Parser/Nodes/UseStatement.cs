using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public class UseStatement
{
    public Token use { get; private init; }
    public Token module { get; private init; }
}