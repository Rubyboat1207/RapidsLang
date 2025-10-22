using RapidsLang.Extensions;
using RapidsLang.Extensions.Pipes;
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
        
        Context.ModuleRegistry.TickExternalModules(Context.GetRoot());

        if (ActiveNode is UseStatementNode useNode)
        {
            Context.ModuleRegistry.TryGetModule(useNode.ModuleIdentifier, out var module);

            if (module is null)
            {
                if (Interpreter.MainSourceCodePath is not null)
                {
                    var sourceDir = Path.GetDirectoryName(Path.GetFullPath(Interpreter.MainSourceCodePath));
                    if (sourceDir is not null)
                    {
                        var relativePath = Path.GetRelativePath(sourceDir, useNode.ModuleIdentifier);
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
                    ProgramCounter++;
                });
            }

            if (exportStatement.ExportNode is FunctionExportable funcExportable)
            {
                Context.Exports.Add(funcExportable.BaseToken.Value,
                    new RapidsFunctionReferenceVariable(
                        new RapidsUserFunction(funcExportable.FunctionNode, new InterpreterContext(Context)
                        )));
                ProgramCounter++;
            }
            
            if (exportStatement.ExportNode is TargetOrSourceExportable targetOrSourceExportable)
            {
                Context.Exports.Add(targetOrSourceExportable.TargetOrSourceNode.Name.Value, AddTargetOrSource(targetOrSourceExportable.TargetOrSourceNode).Variable);
                ProgramCounter++;
            }

            return;
        }

        if (ActiveNode is DefineTargetOrSourceNode targetOrSourceNode)
        {
            AddTargetOrSource(targetOrSourceNode);
            ProgramCounter++;
        }

        if (ActiveNode is FunctionCallStatementNode functionCallStatementNode)
        {
            EvaluateExpression(functionCallStatementNode.Function.Function, function =>
            {
                EvaluateExpressions(functionCallStatementNode.Function.Arguments, variables =>
                {
                    variables.ForEach(Context.FunctionCallStack.Push);

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
                Context.AddVariable(
                    declaration.Name.Value,
                    new VariableHolder(val, declaration.Constant, declaration.Type)
                );
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
            Context.AddVariable(functionDeclaration.Name.Value, new VariableHolder(
                new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionDeclaration.Function, new InterpreterContext(Context))),
                true
            ));
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

    private VariableHolder AddTargetOrSource(DefineTargetOrSourceNode node)
    {
        if (Context.CurrentModule is not ExtensionModule extensionModule)
        {
            throw new Exception("Attempted to define target or source outside of extension module");
        }

        if (extensionModule.Extension.ExtensionManifest.Protocol == null)
        {
            throw new Exception("Attempted to define target or source, but extension does not define a protocol.");
        }

        var holder = new VariableHolder(new RapidsDataInputOutputVariable(
            new DataInputOutput(
                extensionModule,
                new Identifier(extensionModule.Extension.ExtensionManifest.ModuleName,
                    node.Name.Value),
                false,
                true
            )
        ), false);
        
        Context.ModuleRegistry.MarkModuleAsTicking(extensionModule);
        
        Context.AddVariable(node.Name.Value, holder);

        return holder;
    }

    public override bool IsDone()
    {
        return Scope.ProgramCounter >= Block.Statements.Count || Scope.ShouldBreakOut;
    }

    public Node? CurrentlyEvaluatingNode { get; set; }
    public override Node? ActiveNode { get => CurrentlyEvaluatingNode; }

    public override void Cleanup()
    {
        Context.Active = false;
        base.Cleanup();
    }
}