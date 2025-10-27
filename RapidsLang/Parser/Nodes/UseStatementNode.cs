using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record UseStatementNode(
    Token BaseToken,
    ModuleIdent ModuleName,
    List<ImportNode>? ImportNodes,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);

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
}