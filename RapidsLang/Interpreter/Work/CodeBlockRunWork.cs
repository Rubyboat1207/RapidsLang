using RapidsLang.Interpreter.Variables;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record CodeBlockRunWork(BlockProgress Scope, RapidsInterpreter Interpreter, CodeBlockRunWork? Parent) : InterpreterWork(Interpreter, Parent)
{
    private StatementsNode Block => Scope.Block;
    
    private int ProgramCounter
    {
        get => Scope.ProgramCounter;
        set => Scope.ProgramCounter = value;
    }

    protected void EvaluateExpression(ExpressionNode expressionNode, Action<RapidsVariable> callback) =>
        EvaluateExpression(expressionNode, callback, this);

    protected void EvaluateExpressions(List<ExpressionNode> expressionNodes, Action<List<RapidsVariable>> callback) =>
        EvaluateExpressions(expressionNodes, callback, this);
    
    public override void Execute()
    {
        ActiveNode = Block.Statements[ProgramCounter];

        if (ActiveNode is UseStatementNode useNode)
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

        if (ActiveNode is FunctionCallStatementNode functionCallStatementNode)
        {
            EvaluateExpression(functionCallStatementNode.Function.Function, function =>
            {
                EvaluateExpressions(functionCallStatementNode.Function.Arguments, variables =>
                {
                    variables.ForEach(Interpreter.Context.FunctionCallStack.Push);

                    if (function is RapidsFunctionReferenceVariable func)
                    {
                        func.Function.OnCompleted += OnFuncCompleted;
                        
                        func.Function!.EnqueueExecution(Context, this);
                        

                        void OnFuncCompleted()
                        {
                            func.Function.OnCompleted -= OnFuncCompleted;
                            ProgramCounter++;
                        }
                    }
                });

                
            });

            
            return;
        }

        if (ActiveNode is DeclarationNode declaration)
        {
            EvaluateExpression(declaration.Expression, val =>
            {
                Context.variables.Add(
                    declaration.Name.Value,
                    new VariableHolder(val, declaration.Constant, declaration.Type)
                );

                Scope.ScopedVariables.Add(declaration.Name.Value);
                ProgramCounter++;
            });
            

            
            return;
        }

        if (ActiveNode is AssignmentNode assignment)
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
                    ProgramCounter++;
                });
            }, this);
            return;
        }

        if (ActiveNode is WhileLoopNode whileLoopNode)
        {
            EvaluateExpression(whileLoopNode.Condition, exprValue =>
            {
                if (exprValue.Truthy)
                {
                    Interpreter.StartNewBlock(whileLoopNode.Block, BlockType.Loop, this);
                }
                else
                {
                    ProgramCounter++;
                }
            });
            return;
        }

        if (ActiveNode is IfNode ifNode)
        {
            EvaluateExpression(ifNode.Condition, exprValue =>
            {
                if (exprValue.Truthy)
                {
                    Interpreter.StartNewBlock(ifNode.Block, BlockType.Statement, this);
                }
                ProgramCounter++;
            });
            
            return;
        }
        
        if(ActiveNode is FunctionDeclarationNode functionDeclaration)
        {
            Context.variables.Add(functionDeclaration.Name.Value, new VariableHolder(
                new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionDeclaration.Function, Interpreter)),
                true
            ));
            Scope.ScopedVariables.Add(functionDeclaration.Name.Value);
            ProgramCounter++;
            return;
        }

        if (ActiveNode is ReturnNode returnNode)
        {
            EvaluateExpression(returnNode.Value, ret =>
            {
                Scope.Return = ret;
                Scope.ProgramCounter = Block.Statements.Count;

                if(!IsDone())
                {
                    Interpreter.PushWork(new ResumeExecutionWork(BlockType.Function, Interpreter, this));
                }

            });
            return;
        }

        if (ActiveNode is BreakNode)
        {
            Interpreter.PushWork(new ResumeExecutionWork(BlockType.Loop, Interpreter, this));
            ProgramCounter++;
            return;
        }

        if (ActiveNode is ContinueNode)
        {
            ProgramCounter = Block.Statements.Count;
            return;
        }

        if (ActiveNode is ListItemAssignmentNode listItemAssignmentNode)
        {
            EvaluateExpression(listItemAssignmentNode.Array, assignee =>
            {
                EvaluateExpression(listItemAssignmentNode.Index, idx =>
                {
                    EvaluateExpression(listItemAssignmentNode.Value, value =>
                    {
                        var result = value;
                        
                        // this kind of sucks, but whatever.
                        if (assignee is RapidsListVariable list && idx is RapidsNumberVariable num)
                        {
                            if (listItemAssignmentNode.Operator.TokenType != TokenType.Assignment)
                            {
                                result = list.List[(int)num.Value].GetResult(listItemAssignmentNode.Operator.GetOperator(), value);
                            }

                            if (result is null)
                            {
                                throw new Exception("buh");
                            }
                            
                            list.List[(int)num.Value] = result;
                        }
                        else if (assignee is RapidsObjectVariable obj)
                        {
                            obj.ObjectValues[Utils.StringifyVariable(idx)] = value;
                        }
                        else
                        {
                            throw new Exception("Invalid array assignment");
                        }
                        ProgramCounter++;
                    });
                });
            });
            
        }
    }

    public override bool IsDone()
    {
        return Scope.ProgramCounter >= Block.Statements.Count;
    }

    public override Node ActiveNode { get; protected set; }

    public override void Cleanup()
    {
        foreach (var variable in Scope.ScopedVariables)
        {
            Interpreter.Context.variables.Remove(variable);
        }
    }
}