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
    
    public override IEnumerable<ReturnTicket> GetExecution()
    {
        CurrentlyEvaluatingNode = Block.Statements[ProgramCounter];
        
        Context.ModuleRegistry.TickExternalModules(Context.GetRoot());

        switch (ActiveNode)
        {
            case UseStatementNode useNode:
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
                break;
            }
            case ExportStatement exportStatement:
            {
                if (exportStatement.ExportNode is ExpressionExportable exprExport)
                {
                    var evaluation = new NeedExpressionEvaluationReturnTicket(exprExport.Expression);
                    yield return evaluation;

                    if (evaluation.EvaluatedExpression is not null)
                    {
                        Context.Exports.Add(exprExport.BaseToken.Value, evaluation.EvaluatedExpression);
                        ProgramCounter++;
                    }
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

                break;
            }
            case DefineTargetOrSourceNode targetOrSourceNode:
                AddTargetOrSource(targetOrSourceNode);
                ProgramCounter++;
                break;
            case FunctionCallStatementNode functionCallStatementNode:
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
                break;
            case DeclarationNode declaration:
                EvaluateExpression(declaration.Expression, val =>
                {
                    Context.AddVariable(
                        declaration.Name.Value,
                        new VariableHolder(val, declaration.Constant, declaration.Type)
                    );
                    ProgramCounter++;
                });
                break;
            case AssignmentNode assignment:
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
                break;
            case WhileLoopNode whileLoopNode:
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
                break;
            case IfNode ifNode:
            {
                var work = new IfStatementWork(Interpreter, this, ifNode);
                Interpreter.PushWork(work);
            
                work.OnCompleted += WorkOnOnCompleted;

                void WorkOnOnCompleted(InterpreterWork obj)
                {
                    work.OnCompleted -= WorkOnOnCompleted;
                    ProgramCounter++;
                }

                break;
            }
            case FunctionDeclarationNode functionDeclaration:
                Context.AddVariable(functionDeclaration.Name.Value, new VariableHolder(
                    new RapidsFunctionReferenceVariable(new RapidsUserFunction(functionDeclaration.Function, new InterpreterContext(Context))),
                    true
                ));
                ProgramCounter++;
                break;
            case ReturnNode returnNode:
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
                break;
            case OnSourceStatement onSource:
                EvaluateExpression(onSource.Source, sourceVariable =>
                {
                    if (sourceVariable is not RapidsDataChannelVariable channel)
                    {
                        throw new Exception("Attempted to use an on statement with a non channel");
                    }
                
                    if (!channel.Readable)
                    {
                        throw new Exception("Attempted to use an on statement with a channel that does not have a source." +
                                            "\n Either there is no source, and this is the wrong channel" +
                                            "\n Or the extension developer created an undefined target, which you can access using the .on_data function of the channel");
                    }
                
                    channel.SubscribeUsingOnStatement(onSource, Interpreter, this);
                    ProgramCounter++;
                });
                break;
            case BreakNode:
            {
                var codeBlock = this;
            
                while (codeBlock is { Scope.BlockType: not (BlockType.Loop or BlockType.SourceCallback) })
                {
                    codeBlock.Scope.ShouldBreakOut = true;
                    codeBlock = codeBlock.Parent;
                }

                if (codeBlock == null)
                {
                    throw new Exception("Return can only be used under a while loop");
                }
            
                codeBlock.Scope.ShouldBreakOut = true;
                if (codeBlock.Scope.BlockType is BlockType.SourceCallback)
                {
                    codeBlock.Scope.Source?.UnsubscribeOnStatement(codeBlock.Scope.SourceSubscriptionId);
                }

                break;
            }
            case ContinueNode:
                ProgramCounter = Block.Statements.Count;
                break;
            case ListItemAssignmentNode listItemAssignmentNode:
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
                break;
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

        VariableHolder holder;
        
        if (Context.TryFindVariable(node.Name.Value, out var channelVariable))
        {
            if (channelVariable!.Variable is RapidsDataChannelVariable channel)
            {
                if (node.IsTarget)
                {
                    channel.SetWritable();
                }
                else
                {
                    channel.SetReadable();

                    if (node.DataName is not null)
                    {
                        channel.DataVariableName = node.DataName.Value;
                    }
                }

                holder = channelVariable;
            }
            else
            {
                throw new Exception($"variable {node.Name.Value} is already defined in this scope.");
            }
        }
        else
        {
            holder = new VariableHolder(new RapidsDataChannelVariable(
                new DataChannel(
                    extensionModule,
                    new Identifier(
                        extensionModule.Extension.ExtensionManifest.ModuleName,
                        node.Name.Value
                    ),
                    !node.IsTarget,
                    node.IsTarget
                ),
                extensionModule
            ), false);
            
            Context.AddVariable(node.Name.Value, holder);
        }

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