using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record StringNode : ExpressionNode
{
    public Token? EndString;
    public StringNode(Token BaseToken, Token? endString, List<StringPart>? Parts = null) : base(BaseToken)
    {
        this.Parts = Parts ?? [];
        EndString = endString;
    }

    public List<StringPart> Parts { get; init; }
    public override int EndIndex => EndString?.EndIndex ?? Parts.LastOrDefault()?.EndIndex ?? BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => Parts;
}

public abstract record StringPart(Token BaseToken) : Node(BaseToken);

public record LiteralStringPart(Token Value) : StringPart(Value)
{
    public override int EndIndex => Value.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}

public record TemplateStringPart(ExpressionNode Value, Token OpenCurly, Token ClosedCurly) : StringPart(Value.BaseToken)
{
    public override int EndIndex => ClosedCurly.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Value];
}