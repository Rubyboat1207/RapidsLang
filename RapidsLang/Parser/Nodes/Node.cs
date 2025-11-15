using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public abstract record Node(Token BaseToken)
{
    public int StartIndex => BaseToken.Index;
    public abstract int EndIndex { get; }
    public abstract IEnumerable<Node> GetChildren();
}