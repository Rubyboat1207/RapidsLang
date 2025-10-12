using RapidsLang.Interpreter.Variables;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record DefaultExpressionEvaluateWork(ExpressionNode Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter)
    : ExpressionEvaluateWork<ExpressionNode>(Expression, Callback, Interpreter)
{
    public bool _done = false;
    
    public override void Execute()
    {
        switch (Expression)
        {
            case LiteralNumberNode numNode:
                _done = true;
                Callback.Invoke(new RapidsNumberVariable(numNode.Number));
                break;
            case OperationNode operationNode:
                EvaluateExpression(operationNode.Left, left =>
                {
                    EvaluateExpression(operationNode.Right, right =>
                    {
                        var res = left.GetResult(operationNode.Operator.GetOperator(), right);

                        if (res == null)
                        {
                            throw new Exception(
                                $"Operation {operationNode.Operator.GetOperator()} is not compatible with types {left.VariableTypeName} and {right.VariableTypeName}");
                        }

                        Callback.Invoke(res);
                    });
                    
                });
                _done = true;
                break;
            case IdentifierNode identifierNode:
                // ReSharper disable once ConvertIfStatementToReturnStatement
                if (!Context.variables.TryGetValue(identifierNode.Token.Value, out var variable))
                {
                    throw new Exception($"Attempted to access variable \"{identifierNode.Token.Value}\" which is not defined at {GetLineCol(identifierNode.Token)}.");
                }

                _done = true;
                Callback.Invoke(variable.Variable);
                
                break;
            case BooleanNode booleanNode:
                Callback.Invoke(new RapidsBooleanVariable(booleanNode.Value.TokenType == TokenType.True));
                _done = true;
                break;
            case ListNode arrayNode:
                EvaluateExpressions(arrayNode.Values, expressions =>
                {
                    Callback.Invoke(new RapidsListVariable(expressions));
                    
                });
                _done = true;
                break;
            case MemberAccessNode memberAccessNode:
                // ReSharper disable once ConvertIfStatementToReturnStatement
                TryGetValue(memberAccessNode, holder =>
                {
                    if (holder is null)
                    {
                        throw new Exception($"Variable named {memberAccessNode.MemberName} was not found at {GetLineCol(memberAccessNode.MemberName)}");
                    }
                    
                    Callback.Invoke(holder.Variable);
                });

                _done = true;
                break;
            case FunctionNode functionNode:
                _done = true;
                Callback.Invoke(new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionNode, Interpreter)));
                break;
            default:
                throw new NotImplementedException("Expression not supported");
        }
    }

    public override bool IsDone()
    {
        return _done;
    }

    public override void Cleanup()
    {
        
    }
}