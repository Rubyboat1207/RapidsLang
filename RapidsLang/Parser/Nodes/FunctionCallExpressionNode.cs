using System.Linq.Expressions;

namespace RapidsLang.Parser.Nodes;

public record FunctionCallExpressionNode(
    ExpressionNode Function,
    List<ExpressionNode> Arguments
) : ExpressionNode;