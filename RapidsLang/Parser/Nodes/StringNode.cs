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
}

public abstract record StringPart;

public record LiteralStringPart(Token Value) : StringPart;
public record TemplateStringPart(ExpressionNode Value, Token OpenCurly, Token ClosedCurly) : StringPart;