using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionNode(
    Token BaseToken,
    List<ArgumentNode>? Arguments,
    StatementsNode Body,
    StatementsNode? DebugBody=null,
    TypeNode? ReturnType=null
) : ExpressionNode(BaseToken)
{
    public override int EndIndex => DebugBody?.EndIndex ?? Body.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        var list = new List<Node>([Body]);

        if (Arguments is not null)
        {
            list.AddRange(Arguments);
        }

        if (DebugBody is not null)
        {
            list.Add(DebugBody);
        }

        if (ReturnType is not null)
        {
            list.Add(ReturnType);
        }

        return list;
    }
}