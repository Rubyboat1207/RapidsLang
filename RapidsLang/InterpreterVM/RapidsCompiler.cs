using System.Diagnostics.CodeAnalysis;
using RapidsLang.Analyzer;
using RapidsLang.Interpreter;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.InterpreterVM;

public class RapidsCompiler
{
    private readonly List<string> _strings = [];
    private readonly List<ModuleImport> _modules = [];
    private readonly List<Symbol> _definedGlobalSymbols = [];
    private RapidsStaticAnalysisResult _staticAnalysisResult = null!;

    public static RapidProgram Compile(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        return new RapidsCompiler().GenerateProgram(root, staticAnalysisResult);
    }
    
    private RapidProgram GenerateProgram(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        _staticAnalysisResult = staticAnalysisResult;
        var res = CompileStatements(root, []);

        return new RapidProgram
        {
            Header = new BytecodeHeader
            {
                Version = 0,
                Strings = _strings.ToArray(),
                Modules = _modules.ToArray(),
                GlobalsCount = (uint) _definedGlobalSymbols.Count,
                OutermostLocalsCount = res.LocalsUsed
            },
            Code = res.Operations.ToArray()
        };
    }

    private struct CompileStatementsResult
    {
        public uint LocalsUsed;
        public List<OpCode> Operations;
    }
    
    private CompileStatementsResult CompileStatements(StatementsNode root, List<Symbol> definedSymbols, int startIndex=0, uint locals=0)
    {
        var localsUsed = locals;
        List<OpCode> operations = [];
        foreach (var statement in root.Statements)
        {
            switch (statement)
            {
                case FunctionCallStatementNode functionCall:
                {
                    operations.AddRange(CompileFunctionCall(functionCall.Function, definedSymbols));
                    break;
                }
                case DeclarationNode declarationNode:
                {
                    operations.AddRange(CompileExpression(declarationNode.Expression, definedSymbols));
                    operations.Add(new StoreLocal((int) localsUsed++));
                    definedSymbols.Add(_staticAnalysisResult.SymbolReferences[declarationNode.Name]);
                    break;
                }
                case UseStatementNode useStatementNode:
                {
                    _modules.Add(new ModuleImport(
                        useStatementNode.ModuleName.GetName(),
                        useStatementNode.ImportNodes.Select(i => i.BaseToken.Value).ToArray())
                    );
                    if (useStatementNode.ImportNodes is null)
                    {
                        _definedGlobalSymbols.AddRange(_staticAnalysisResult.ImplicitlyImportedSymbols[useStatementNode]);
                    }
                    else
                    {
                        foreach (var importNode in useStatementNode.ImportNodes)
                        {
                            _definedGlobalSymbols.Add(_staticAnalysisResult.ExplicitlyImportedSymbols[importNode]);
                        }
                    }
                    
                    break;
                }
                case IfNode ifNode:
                {
                    operations.AddRange(CompileExpression(ifNode.Condition, definedSymbols));
                    {
                        var block = CompileStatements(ifNode.Block, definedSymbols, startIndex + operations.Count, localsUsed);
                        // +2 b/c this expression is 1 and the jump to end is another
                        operations.Add(new JumpIfFalse(startIndex + operations.Count + block.Operations.Count + 2)); 
                        localsUsed += block.LocalsUsed;
                        operations.AddRange(block.Operations);
                    }

                    
                    var opsToReplace = new List<OpCode>();
                    if (ifNode.ElseNodes.Count > 0)
                    {
                        var endOfInitialBlock = new NoOp();
                        operations.Add(endOfInitialBlock);
                        opsToReplace.Add(endOfInitialBlock);
                    }
                    
                    var endIndex = 0;
                    foreach (var eNode in ifNode.ElseNodes)
                    {
                        var block = CompileStatements(eNode.Block, definedSymbols, startIndex + operations.Count, localsUsed);

                        if (eNode.Condition is not null)
                        {
                            operations.AddRange(CompileExpression(eNode.Condition, definedSymbols));
                            operations.Add(new JumpIfFalse(startIndex + operations.Count + block.Operations.Count + 2));
                        }
                        
                        localsUsed += block.LocalsUsed;
                        operations.AddRange(block.Operations);
                        var endOf = new NoOp();
                        operations.Add(endOf);
                        opsToReplace.Add(endOf);
                    }

                    foreach (var index in opsToReplace.Select(opCode => operations.IndexOf(opCode)))
                    {
                        operations.RemoveAt(index);
                        
                        operations.Insert(index, new Jump(startIndex + operations.Count));
                    }
                    operations.RemoveAt(operations.Count - 1);
                    break;
                }
            }
        }

        return new CompileStatementsResult { LocalsUsed = localsUsed, Operations = operations };
    }

    private List<OpCode> CompileFunctionCall(FunctionCallExpressionNode callExpressionNode, List<Symbol> definedSymbols)
    {
        List<OpCode> operations = [];
        foreach (var arg in callExpressionNode.Arguments)
        {
            operations.AddRange(CompileExpression(arg, definedSymbols));
        }
        operations.AddRange(CompileExpression(callExpressionNode.Function, definedSymbols));
        operations.Add(new Call());

        return operations;
    }

    private List<OpCode> CompileExpression(ExpressionNode expressionNode, List<Symbol> definedSymbols)
    {
        List<OpCode> operations = [];
        switch (expressionNode)
        {
            case IdentifierNode identifierNode:
            {
                if (_staticAnalysisResult.SymbolReferences.TryGetValue(identifierNode, out var symbol))
                {
                    if (definedSymbols.Contains(symbol))
                    {
                        return [new LoadLocal(definedSymbols.IndexOf(symbol))];
                    }

                    if (_definedGlobalSymbols.Contains(symbol))
                    {
                        return [new LoadGlobal(_definedGlobalSymbols.IndexOf(symbol))];
                    }

                    if (symbol.Name == "exit")
                    {
                        return [new Exit()];
                    }
                    
                    // undefined reference
                }
                break;
            }
            case LiteralNumberNode literalNumberNode:
            {
                return [new LoadNumber(literalNumberNode.Number)];
            }
            case BooleanNode booleanNode:
            {
                return [new LoadBool(booleanNode.Value.TokenType is TokenType.True)];
            }
            case StringNode stringNode:
            {
                
                foreach (var part in stringNode.Parts)
                {
                    switch (part)
                    {
                        case LiteralStringPart lit when !_strings.Contains(lit.Value.Value):
                            _strings.Add(lit.Value.Value);
                            operations.Add(new LoadString(_strings.Count - 1));
                            break;
                        case LiteralStringPart lit:
                            operations.Add(new LoadString(_strings.IndexOf(lit.Value.Value)));
                            break;
                        case TemplateStringPart template:
                            operations.AddRange(CompileExpression(template.Value, definedSymbols));
                            break;
                    }
                }

                if (stringNode.Parts.Count > 1)
                {
                    operations.Add(new Concat(stringNode.Parts.Count));
                }
                break;
            }
        }

        return operations;
    }
}