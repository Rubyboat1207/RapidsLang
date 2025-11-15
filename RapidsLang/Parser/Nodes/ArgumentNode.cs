using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ArgumentNode(
    Token Name,
    TypeNode? Type
) : Node(Name)
{
    public override int EndIndex => Type?.EndIndex ?? Name.EndIndex;

    public override IEnumerable<Node> GetChildren() => [];
}