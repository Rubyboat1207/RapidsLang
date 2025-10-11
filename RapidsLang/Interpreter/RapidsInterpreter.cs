using RapidsLang.Lexer;
using RapidsLang.PreProcessor;

namespace RapidsLang.Interpreter;

using Parser.Nodes;

public class BlockProgress(StatementsNode block, int programCounter=0)
{
    public int ProgramCounter { get; set; } = programCounter;
    public readonly List<string> ScopedVariables = [];
    // Todo: Closures
    public StatementsNode Block { get; } = block;
    public RapidsVariable? Return = null;
}

public abstract record InterpreterWork(RapidsInterpreter interpreter)
{
    public RapidsInterpreter Interpreter = interpreter;
    
    protected InterpreterContext Context => Interpreter.Context;
    protected string GetLineCol(Token token) => Interpreter.GetLineCol(token);
    
    public abstract void Execute();
    public abstract bool IsDone();
    public virtual void Cleanup() {}
    public List<Action> CompletedListeners = [];

    protected void EvaluateExpression(ExpressionNode expressionNode, Action<RapidsVariable> callback)
    {
        if (expressionNode is FunctionCallExpressionNode fcen)
        {
            Interpreter.PushWork(new FunctionCallExpressionEvaluateWork(fcen, callback, Interpreter));
            return;
        }
        if (expressionNode is StringNode str)
        {
            Interpreter.PushWork(new StringExpressionEvaluateWork(str, callback, Interpreter));
            return;
        }
        Interpreter.PushWork(new DefaultExpressionEvaluateWork(expressionNode, callback, Interpreter));
    }

    protected void EvaluateExpressions(List<ExpressionNode> expressionNodes, Action<List<RapidsVariable>> callback)
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
            });
        }
        if(expressionNodes.Count == 0)
        {
            callback.Invoke(variables);
        }
    }

    protected void TryGetValue(MemberAccessNode accessNode, Action<VariableHolder?> rapidsVariableCallback)
    {
        if (accessNode.Left is null)
        {
            Context.variables.TryGetValue(accessNode.MemberName.Value, out var rapidsVariable);
            rapidsVariableCallback.Invoke(rapidsVariable);
            return;
        }
        
        EvaluateExpression(accessNode.Left, left =>
        {
            var value = left.GetMember(accessNode.MemberName.Value);

            if (value is null)
            {
                rapidsVariableCallback.Invoke(null);
                return;
            }

            rapidsVariableCallback.Invoke(new VariableHolder(value, false));
        });
    }
    
    protected void TryGetValue(IdentifierNode identifierNode, Action<VariableHolder?> rapidsVariableCallback)
    {
        Context.variables.TryGetValue(identifierNode.Token.Value, out var rapidsVariable);
        rapidsVariableCallback.Invoke(rapidsVariable);
    }
}

public record CodeBlockRunWork(BlockProgress Scope, RapidsInterpreter Interpreter) : InterpreterWork(Interpreter)
{
    private StatementsNode Block => Scope.Block;
    
    private int ProgramCounter
    {
        get => Scope.ProgramCounter;
        set => Scope.ProgramCounter = value;
    }
    
    public override void Execute()
    {
        StatementNode statement = Block.Statements[ProgramCounter];

        if (statement is UseStatementNode useNode)
        {
            Modules.RegisteredModules.TryGetValue(useNode.ModuleIdentifier, out var module);

            if (module is null)
            {
                Console.WriteLine($"Module {useNode.ModuleIdentifier} at {GetLineCol(useNode.Use)} was not found.");
            }
            else
            {
                module.Import(Context);
            }
            ProgramCounter++;
            return;
        }

        if (statement is FunctionCallStatementNode functionCallStatementNode)
        {
            EvaluateExpression(functionCallStatementNode.Function.Function, function =>
            {
                EvaluateExpressions(functionCallStatementNode.Function.Arguments, variables =>
                {
                    variables.ForEach(Interpreter.Context.FunctionCallStack.Push);

                    if (function is RapidsFunctionReferenceVariable func)
                    {
                        func.Function!.EnqueueExecution(Context);
                    }
                });

                ProgramCounter++;
            });

            
            return;
        }

        if (statement is DeclarationNode declaration)
        {
            EvaluateExpression(declaration.Expression, val =>
            {
                Context.variables.Add(
                    declaration.Name.Value,
                    new VariableHolder(val, declaration.Constant, declaration.Type)
                );

                Scope.ScopedVariables.Add(declaration.Name.Value);
            });
            

            ProgramCounter++;
            return;
        }

        if (statement is AssignmentNode assignment)
        {
            if (assignment.Variable.Left == null)
            {

                TryGetValue(assignment.Variable, variable =>
                {
                    if (variable is null)
                        throw new Exception("Variable was undefined");
                    if (variable!.Constant)
                        throw new Exception($"attempted to assign to a constant variable at {GetLineCol(assignment.Operator)}.");

                    EvaluateExpression(assignment.Expression, evaluatedExpression =>
                    {
                        var result = evaluatedExpression;
                
                        if(assignment.Operator.TokenType != TokenType.Assignment)
                        {
                            result = variable!.Variable.GetResult(assignment.Operator.GetOperator(), evaluatedExpression);
                        }

                        if(result is null)
                        {
                            throw new Exception(
                                $"Operation {assignment.Operator.GetOperator()} is not compatible with types {variable!.Variable.VariableTypeName} and {evaluatedExpression.VariableTypeName}");
                        }

                        variable!.Variable = result;
                    });
                });
            }
            ProgramCounter++;
            return;
        }

        if (statement is WhileLoopNode whileLoopNode)
        {
            EvaluateExpression(whileLoopNode.Condition, exprValue =>
            {
                if (exprValue.Truthy)
                {
                    Interpreter.StartNewBlock(whileLoopNode.Block);
                }
                else
                {
                    ProgramCounter++;
                }
            });
            return;
        }

        if (statement is IfNode ifNode)
        {
            EvaluateExpression(ifNode.Condition, exprValue =>
            {
                if (exprValue.Truthy)
                {
                    Interpreter.StartNewBlock(ifNode.Block);
                }
            });
            ProgramCounter++;
            return;
        }
        
        if(statement is FunctionDeclarationNode functionDeclaration)
        {
            Context.variables.Add(functionDeclaration.Name.Value, new VariableHolder(
                new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionDeclaration.Function, Interpreter)),
                true
            ));
            ProgramCounter++;
            return;
        }

        if (statement is ReturnNode returnNode)
        {
            EvaluateExpression(returnNode.Value, ret =>
            {
                Scope.ProgramCounter = Block.Statements.Count;
                Scope.Return = ret;
            });
        }
    }

    public override bool IsDone()
    {
        return Scope.ProgramCounter >= Block.Statements.Count;
    }

    public override void Cleanup()
    {
        foreach (var variable in Scope.ScopedVariables)
        {
            Interpreter.Context.variables.Remove(variable);
        }
    }
}

public abstract record ExpressionEvaluateWork<T>(T Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter)
    : InterpreterWork(Interpreter) where T : ExpressionNode
{
    
}

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
                Callback.Invoke(new RapidsBooleanVariable(booleanNode.value.TokenType == TokenType.True));
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

public record FunctionCallExpressionEvaluateWork(FunctionCallExpressionNode Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter) 
    : ExpressionEvaluateWork<FunctionCallExpressionNode>(Expression, Callback, Interpreter)
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
                    
                    func.Function.EnqueueExecution(Context);
                        
                });
            });
            _enqueued = true;
            return;
        }
        // if I'm back, that must mean its already ran!
        Callback.Invoke(Context.FunctionCallStack.Pop());
        _done = true;
    }

    public override bool IsDone()
    {
        return _done;
    }

    public override void Cleanup()
    {
        
    }
}

public record StringExpressionEvaluateWork(StringNode Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter)
    : ExpressionEvaluateWork<StringNode>(Expression, Callback, Interpreter)
{
    private string _str = "";
    private int partIndex;
    private bool _done = false;
    public override void Execute()
    {
        for (; partIndex < Expression.Parts.Count; partIndex++)
        {
            var part = Expression.Parts[partIndex];
            switch (part)
            {
                case LiteralStringPart lit:
                    _str += lit.Value.Value;
                    break;
                case TemplateStringPart template:
                    EvaluateExpression(template.Value, val =>
                    {
                        _str += Utils.StringifyVariable(val);
                    });
                    partIndex++;
                    return;
            }
        }
        _done = true;
        Callback.Invoke(new RapidsStringVariable(_str));
    }

    public override bool IsDone()
    {
        return _done;
    }
}

public class RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, StatementsNode root)
{
    public InterpreterContext Context { get; } = new();
    private readonly Stack<InterpreterWork> _workStack = [];

    public void PushWork(InterpreterWork work)
    {
        _workStack.Push(work);
    }

    public string GetLineCol(Token token)
    {
        return RapidsPreproc.GetRowCol(sourceCode, token.Index, preprocessorMetadata);
    }

    public CodeBlockRunWork StartNewBlock(StatementsNode block)
    {
        CollapseStack();
        var progress = new BlockProgress(block);
        var work = new CodeBlockRunWork(progress, this);
        _workStack.Push(work);

        return work;
    }

    public void Interpret()
    {
        StartNewBlock(root);
        while (true)
        {
            CollapseStack();
            if (_workStack.TryPeek(out var work))
            {
                work.Execute();
            }
            else
            {
                break;
            }

        }
    }
    
    public void CollapseStack()
    {
        while(_workStack.TryPeek(out var work) && work.IsDone())
        {
            _workStack.Pop().Cleanup();
            work.CompletedListeners.ForEach(l => l.Invoke());
            continue;
        }
    }
}