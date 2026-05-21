using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record OnSourceStatement(
    Token BaseToken,
    ExpressionNode Source,
    TimingNode? Every,
    StatementsNode Body,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => Body.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        if (Every is null)
        {
            return [Source, Body];
        }
        return [Source, Every, Body];
    }
}