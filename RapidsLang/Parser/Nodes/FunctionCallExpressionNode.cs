using System.Linq.Expressions;

namespace RapidsLang.Parser.Nodes;

public record FunctionCallExpressionNode(
    ExpressionNode Function,
    List<ExpressionNode> Arguments
) : ExpressionNode(Function.BaseToken)
{
    public override int EndIndex => Arguments.LastOrDefault()?.EndIndex ?? Function.EndIndex;
    public override IEnumerable<Node> GetChildren()
    {
        var list = new List<Node>([Function]);
        
        list.AddRange(Arguments);

        return list;
    }
}