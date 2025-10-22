using RapidsLang.Interpreter.Variables;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record DefaultExpressionEvaluateWork(ExpressionNode Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter, CodeBlockRunWork? Parent)
    : ExpressionEvaluateWork<ExpressionNode>(Expression, Callback, Interpreter, Parent)
{
    private bool _done;
    
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
                if (!Context.TryFindVariable(identifierNode.Token.Value, out var variable))
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
                }, Parent!);

                _done = true;
                break;
            case FunctionNode functionNode:
                _done = true;
                Callback.Invoke(new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionNode, new InterpreterContext(Context))));
                break;
            case ObjectNode objectNode:
                Dictionary<string, RapidsVariable> keyValues = [];
                _done = true;
                if (objectNode.keyValues.Count == 0)
                {
                    Callback.Invoke(new RapidsObjectVariable());
                }
                
                objectNode.keyValues.ForEach(kv =>
                {
                    EvaluateExpression(kv.Item1, key =>
                    {
                        if (key is not RapidsStringVariable str)
                        {
                            throw new Exception("Key must be a string");
                        }
                        EvaluateExpression(kv.Item2, value =>
                        {
                            keyValues[str.Value] = value;

                            if (keyValues.Count == objectNode.keyValues.Count)
                            {
                                Callback.Invoke(new RapidsObjectVariable(keyValues));
                            } 
                        });
                    });
                });
                break;
            default:
                throw new NotImplementedException("Expression not supported");
        }
    }

    public override bool IsDone()
    {
        return _done;
    }
}