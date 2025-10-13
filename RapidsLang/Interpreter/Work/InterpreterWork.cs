using RapidsLang.Interpreter.Variables;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public abstract record InterpreterWork(RapidsInterpreter interpreter, CodeBlockRunWork? Parent)
{
    public RapidsInterpreter Interpreter = interpreter;
    
    protected InterpreterContext Context => Interpreter.Context;
    protected string GetLineCol(Token token) => Interpreter.GetLineCol(token);
    
    public abstract void Execute();
    public abstract bool IsDone();
    public abstract Node ActiveNode { get; protected set; }
    public virtual void Cleanup() {}
    public List<Action> CompletedListeners = [];

    protected void EvaluateExpression(ExpressionNode expressionNode, Action<RapidsVariable> callback, CodeBlockRunWork parent)
    {
        if (expressionNode is FunctionCallExpressionNode fcen)
        {
            Interpreter.PushWork(new FunctionCallExpressionEvaluateWork(fcen, callback, Interpreter, parent));
            return;
        }
        if (expressionNode is StringNode str)
        {
            Interpreter.PushWork(new StringExpressionEvaluateWork(str, callback, Interpreter, parent));
            return;
        }
        Interpreter.PushWork(new DefaultExpressionEvaluateWork(expressionNode, callback, Interpreter, parent));
    }

    protected void EvaluateExpressions(List<ExpressionNode> expressionNodes, Action<List<RapidsVariable>> callback, CodeBlockRunWork parent)
    {
        List<RapidsVariable> variables = [];

        for (var i = 0; i < expressionNodes.Count; i++)
        {
            var node = expressionNodes[expressionNodes.Count - 1 - i];

            EvaluateExpression(node, v =>
            {
                variables.Add(v);

                if (variables.Count == expressionNodes.Count)
                {
                    callback.Invoke(variables);
                }
            }, parent);
        }
        if(expressionNodes.Count == 0)
        {
            callback.Invoke(variables);
        }
    }

    protected void TryGetValue(MemberAccessNode accessNode, Action<VariableHolder?> rapidsVariableCallback, CodeBlockRunWork parent)
    {
        if (accessNode.Left is null)
        {
            Context.variables.TryGetValue(accessNode.MemberName.Value, out var rapidsVariable);
            rapidsVariableCallback.Invoke(rapidsVariable);
            return;
        }
        
        EvaluateExpression(accessNode.Left, left =>
        {
            if (left is RapidsObjectVariable objectNode)
            {
                rapidsVariableCallback.Invoke(objectNode.GetMemberReference(accessNode.MemberName.Value));
                return;
            }
            var value = left.GetMember(accessNode.MemberName.Value);

            if (value is null)
            {
                rapidsVariableCallback.Invoke(null);
                return;
            }
            
            rapidsVariableCallback.Invoke(new VariableHolder(value, false));
        }, parent);
    }
    
    protected void TryGetValue(IdentifierNode identifierNode, Action<VariableHolder?> rapidsVariableCallback)
    {
        Context.variables.TryGetValue(identifierNode.Token.Value, out var rapidsVariable);
        rapidsVariableCallback.Invoke(rapidsVariable);
    }
}

public abstract record ExpressionEvaluateWork<T>(
    T Expression,
    Action<RapidsVariable> Callback,
    RapidsInterpreter Interpreter,
    CodeBlockRunWork? Parent
)
    : InterpreterWork(Interpreter, Parent) where T : ExpressionNode
{
    public override Node ActiveNode { get; protected set; } = Expression;
    
    protected void EvaluateExpression(ExpressionNode expressionNode, Action<RapidsVariable> callback) =>
        EvaluateExpression(expressionNode, callback, Parent!);

    protected void EvaluateExpressions(List<ExpressionNode> expressionNodes, Action<List<RapidsVariable>> callback) =>
        EvaluateExpressions(expressionNodes, callback, Parent!);
}