using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ImportNode(
    Token BaseToken,
    Token? AsName
) : Node(BaseToken)
{
    public override int EndIndex => AsName?.EndIndex ?? BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}