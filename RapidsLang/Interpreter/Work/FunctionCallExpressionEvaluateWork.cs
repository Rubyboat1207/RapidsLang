using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record FunctionCallExpressionEvaluateWork(FunctionCallExpressionNode Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter, CodeBlockRunWork? Parent) 
    : ExpressionEvaluateWork<FunctionCallExpressionNode>(Expression, Callback, Interpreter, Parent)
{
    private bool _enqueued;
    private bool _done;
    public override void Execute()
    {
        if (!_enqueued)
        {
            EvaluateExpression(Expression.Function, funcVar =>
            {
                if (funcVar is not RapidsFunctionReferenceVariable func)
                {
                    throw new Exception("Attempted to call non function");
                }
                
                EvaluateExpressions(Expression.Arguments, args =>
                {
                    args.ForEach(Context.FunctionCallStack.Push);

                    void OnFunctionOnOnCompleted()
                    {
                        func.Function.OnCompleted -= OnFunctionOnOnCompleted;
                        _done = true;
                        Callback.Invoke(Context.FunctionCallStack.Pop());
                    }

                    func.Function.OnCompleted += OnFunctionOnOnCompleted;

                    func.Function.EnqueueExecution(Context, Parent);
                });
            });
            _enqueued = true;
            return;
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