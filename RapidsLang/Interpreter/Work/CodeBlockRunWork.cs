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

    private void EvaluateExpression(ExpressionNode expressionNode, Action<RapidsVariable> callback) =>
        EvaluateExpression(expressionNode, callback, this);

    private void EvaluateExpressions(List<ExpressionNode> expressionNodes, Action<List<RapidsVariable>> callback) =>
        EvaluateExpressions(expressionNodes, callback, this);
    
    public override void Execute()
    {
        CurrentlyEvaluatingNode = Block.Statements[ProgramCounter];

        if (ActiveNode is UseStatementNode useNode)
        {
            Context.ModuleRegistry.TryGetModule(useNode.ModuleIdentifier, out var module);

            if (module is null)
            {
                if (Interpreter.MainSourceCodePath is not null)
                {
                    var relativePath = Path.GetRelativePath(Interpreter.MainSourceCodePath, useNode.ModuleIdentifier);

                    string? filePath = null;
                    if (File.Exists(relativePath))
                    {
                        filePath = relativePath;
                    }else if (File.Exists(relativePath + ".rpd"))
                    {
                        filePath = relativePath + ".rpd";
                    }

                    if (filePath is not null)
                    {
                        module = new CodeModule(File.ReadAllText(relativePath), filePath);
                    }
                }
            }

            if (module is null)
            {
                Console.WriteLine($"Module {useNode.ModuleIdentifier} at {GetLineCol(useNode.BaseToken)} was not found.");
            }
            else
            {
                module.Import(Context, useNode.ImportNodes);
            }
            ProgramCounter++;
            return;
        }

        if (ActiveNode is ExportStatement exportStatement)
        {
            if (exportStatement.ExportNode is ExpressionExportable exprExport)
            {
                EvaluateExpression(exprExport.Expression, val =>
                {
                    Context.Exports.Add(exprExport.BaseToken.Value, val);
                });
            }

            if (exportStatement.ExportNode is FunctionExportable funcExportable)
            {
                Context.Exports.Add(funcExportable.BaseToken.Value, new RapidsFunctionReferenceVariable(new RapidsUserFunction(funcExportable.FunctionNode)));
            }

            return;
        }

        if (ActiveNode is FunctionCallStatementNode functionCallStatementNode)
        {
            EvaluateExpression(functionCallStatementNode.Function.Function, function =>
            {
                EvaluateExpressions(functionCallStatementNode.Function.Arguments, variables =>
                {
                    variables.ForEach(Interpreter.Context.FunctionCallStack.Push);

                    if (function is not RapidsFunctionReferenceVariable func) return;
                    func.Function.OnCompleted += OnFuncCompleted;
                        
                    func.Function.EnqueueExecution(Interpreter, this);
                    return;


                    void OnFuncCompleted()
                    {
                        func.Function.OnCompleted -= OnFuncCompleted;
                        ProgramCounter++;
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
                if (variable.Constant)
                    throw new Exception($"attempted to assign to a constant variable at {GetLineCol(assignment.Operator)}.");

                EvaluateExpression(assignment.Expression, evaluatedExpression =>
                {
                    var result = evaluatedExpression;
                
                    if(assignment.Operator.TokenType != TokenType.Assignment)
                    {
                        result = variable.Variable.GetResult(assignment.Operator.GetOperator(), evaluatedExpression);
                    }

                    if(result is null)
                    {
                        throw new Exception(
                            $"Operation {assignment.Operator.GetOperator()} is not compatible with types {variable.Variable.VariableTypeName} and {evaluatedExpression.VariableTypeName}");
                    }
                    

                    variable.Variable = result;
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
                    var block = Interpreter.StartNewBlock(whileLoopNode.Block, BlockType.Loop, this);
                    // Console.WriteLine("Adding listener to block " + block.GetHashCode());
                    
                    block.OnCompleted += BlockOnCompletedListeners;

                    void BlockOnCompletedListeners(InterpreterWork work)
                    {
                        if(work is not CodeBlockRunWork codeWork)
                        {
                            throw new Exception("how");
                        }

                        if (codeWork.Scope.ShouldBreakOut)
                        {
                            ProgramCounter++;
                        }
                    }
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
            // EvaluateExpression(ifNode.Condition, exprValue =>
            // {
            //     if (exprValue.Truthy)
            //     {
            //         Interpreter.StartNewBlock(ifNode.Block, BlockType.Statement, this);
            //     }
            //     ProgramCounter++;
            // });
            var work = new IfStatementWork(Interpreter, this, ifNode);
            Interpreter.PushWork(work);
            
            work.OnCompleted += WorkOnOnCompleted;

            void WorkOnOnCompleted(InterpreterWork obj)
            {
                work.OnCompleted -= WorkOnOnCompleted;
                ProgramCounter++;
            }

            return;
        }
        
        if(ActiveNode is FunctionDeclarationNode functionDeclaration)
        {
            Context.variables.Add(functionDeclaration.Name.Value, new VariableHolder(
                new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionDeclaration.Function)),
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
                var codeBlock = this;
            
                while (codeBlock is { Scope.BlockType: not BlockType.Function })
                {
                    codeBlock.Scope.ShouldBreakOut = true;
                    codeBlock = codeBlock.Parent;
                }

                if (codeBlock == null)
                {
                    throw new Exception("Return can only be used under a while loop");
                }
            
                codeBlock.Scope.ShouldBreakOut = true;
                Context.FunctionCallStack.Push(ret);
            });
            return;
        }

        if (ActiveNode is BreakNode)
        {
            // Interpreter.PushWork(new ResumeExecutionWork(BlockType.Loop, Interpreter, this));
            // ProgramCounter++;
            var codeBlock = this;
            
            while (codeBlock is { Scope.BlockType: not BlockType.Loop })
            {
                codeBlock.Scope.ShouldBreakOut = true;
                codeBlock = codeBlock.Parent;
            }

            if (codeBlock == null)
            {
                throw new Exception("Return can only be used under a while loop");
            }
            
            codeBlock.Scope.ShouldBreakOut = true;
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
        return Scope.ProgramCounter >= Block.Statements.Count || Scope.ShouldBreakOut;
    }

    public Node? CurrentlyEvaluatingNode { get; set; }
    public override Node? ActiveNode { get => CurrentlyEvaluatingNode; }

    public override void Cleanup()
    {
        foreach (var variable in Scope.ScopedVariables)
        {
            Interpreter.Context.variables.Remove(variable);
        }
        
        base.Cleanup();
    }
}