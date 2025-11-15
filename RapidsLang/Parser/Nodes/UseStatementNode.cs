using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record UseStatementNode(
    Token BaseToken,
    ModuleIdent ModuleName,
    List<ImportNode>? ImportNodes,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => ImportNodes?.LastOrDefault()?.EndIndex ?? ModuleName.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        var list = new List<Node>([ModuleName]);

        if (ImportNodes is not null)
        {
            list.AddRange(ImportNodes);
        }

        return list;
    }
}

public abstract record ModuleIdent(Token BaseToken) : Node(BaseToken)
{
    public abstract string GetName();
}

public record LiteralModuleIdentifier(Token BaseToken, List<Token> Tokens) : ModuleIdent(BaseToken)
{
    public override string GetName()
    {
        return Tokens.Aggregate("", (current, token) => current + token.Value);
    }

    public override int EndIndex => Tokens.LastOrDefault()?.EndIndex ?? BaseToken.EndIndex;
    public override IEnumerable<Node> GetChildren() => [];
}

public record StringModuleIdent(StringNode StringNode) : ModuleIdent(StringNode.BaseToken)
{
    public override string GetName()
    {
        var str = "";
        
        foreach(var part in StringNode.Parts)
        {
            if (part is LiteralStringPart literalStringPart)
            {
                str += literalStringPart.Value;
            }
        }

        return str;
    }

    public override int EndIndex => StringNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [StringNode];
}