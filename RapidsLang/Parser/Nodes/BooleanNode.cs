using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record BooleanNode(
    Token Value
) : ExpressionNode(Value)
{
    public override int EndIndex => Value.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}