using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record StringNode : ExpressionNode
{
    public StringNode(Token StartString, List<StringPart>? Parts = null) : base(StartString)
    {
        this.Parts = Parts ?? [];
    }

    public List<StringPart> Parts { get; init; }
}

public abstract record StringPart;

public record LiteralStringPart(Token Value) : StringPart;
public record TemplateStringPart(ExpressionNode Value) : StringPart;