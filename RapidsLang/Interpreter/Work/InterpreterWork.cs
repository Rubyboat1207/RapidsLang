using RapidsLang.Interpreter.Variables;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public abstract class ReturnTicket;

public class CompletedReturnTicket : ReturnTicket;

public class ContinueReturnTicket : ReturnTicket;

public class YieldReturnTicket : ReturnTicket;

public class NeedExpressionEvaluationReturnTicket(ExpressionNode expressionNode) : ReturnTicket
{
    public ExpressionNode ExpressionNode { get; } = expressionNode;
    public RapidsVariable? EvaluatedExpression { get; set; }
}

public abstract record InterpreterWork
{
    protected InterpreterWork(RapidsInterpreter Interpreter, CodeBlockRunWork? Parent)
    {
        this.Interpreter = Interpreter;
        this.Parent = Parent;
        
        // this should get the current active context, and won't change as it updates.
        Context = Interpreter.Context; 
    }

    protected InterpreterContext Context { get; init; }
    protected string GetLineCol(Token token) => Interpreter.GetLineCol(token);
    
    public abstract IEnumerable<ReturnTicket> GetExecution();
    public abstract bool IsDone();
    public abstract Node? ActiveNode { get; }
    public RapidsInterpreter Interpreter { get; init; }
    public CodeBlockRunWork? Parent { get; init; }

    public virtual void Cleanup()
    {
        // Console.WriteLine("Invoking listeners on block " + this.GetHashCode());
        OnCompleted?.Invoke(this);
    }
    public event Action<InterpreterWork>? OnCompleted;

    protected void TryGetValue(MemberAccessNode accessNode, Action<VariableHolder?> rapidsVariableCallback, CodeBlockRunWork parent)
    {
        if (accessNode.Left is null)
        {
            Context.TryFindVariable(accessNode.MemberName.Value, out var rapidsVariable);
            rapidsVariableCallback.Invoke(rapidsVariable);
            yield break;
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

    public void Deconstruct(out RapidsInterpreter Interpreter, out CodeBlockRunWork? Parent)
    {
        Interpreter = this.Interpreter;
        Parent = this.Parent;
    }
}

public abstract record ExpressionEvaluateWork<T>(
    T Expression,
    NeedExpressionEvaluationReturnTicket ReturnTicket,
    RapidsInterpreter Interpreter,
    CodeBlockRunWork? Parent
)
    : InterpreterWork(Interpreter, Parent) where T : ExpressionNode
{
    public override Node? ActiveNode { get; } = Expression;
}