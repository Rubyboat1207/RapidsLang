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
    private readonly List<RapidsBytecodeFunction> _functions = [];
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
            Code = res.Operations.ToArray(),
            FunctionBlock = new RapidsProgramFunctionBlock
            {
                Functions = _functions.ToArray()
            }
        };
    }

    private class CompileStatementsResult
    {
        public uint LocalsUsed;
        public List<OpCode> Operations = [];

        public List<OpCode> BreakPlaceholders = [];

        public List<OpCode> ContinuePlaceholders = [];
    }

    enum StatementCompilationContext
    {
        OuterScope,
        Function,
        Loop,
        DataSource
    }
    
    private CompileStatementsResult CompileStatements(
        StatementsNode root,
        List<Symbol> definedSymbols,
        int startIndex=0,
        StatementCompilationContext compilationContext=StatementCompilationContext.OuterScope
    )
    {
        var sResult = new CompileStatementsResult();
        foreach (var statement in root.Statements)
        {
            switch (statement)
            {
                case FunctionCallStatementNode functionCall:
                {
                    var res = CompileFunctionCall(functionCall.Function, definedSymbols);
                    sResult.Operations.AddRange(res.OpCodes);
                    break;
                }
                case DeclarationNode declarationNode:
                {
                    var res = CompileExpression(declarationNode.Expression, definedSymbols);
                    sResult.Operations.AddRange(res.OpCodes);
                    sResult.Operations.Add(new StoreLocal((int) sResult.LocalsUsed++));
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
                case FunctionDeclarationNode functionDeclarationNode:
                {
                    var res = CompileExpression(functionDeclarationNode.Function, definedSymbols);
                    sResult.Operations.AddRange(res.OpCodes);
                    sResult.Operations.Add(new StoreLocal((int) sResult.LocalsUsed++));
                    definedSymbols.Add(_staticAnalysisResult.SymbolReferences[functionDeclarationNode.Name]);
                    break;
                }
                case ReturnNode returnNode:
                {
                    if (returnNode.Value is null)
                    {
                        sResult.Operations.AddRange([new LoadBool(false), new Return()]);
                    }
                    else
                    {
                        var res = CompileExpression(returnNode.Value, definedSymbols);
                        sResult.Operations.AddRange(res.OpCodes);
                        sResult.Operations.AddRange([new LoadBool(true), new Return()]);
                    }
                    break;
                }
                case IfNode ifNode:
                {
                    var res = CompileExpression(ifNode.Condition, definedSymbols);
                    sResult.Operations.AddRange(res.OpCodes);
                    {
                        var block = CompileStatements(ifNode.Block, definedSymbols, startIndex + sResult.Operations.Count);
                        sResult.Operations.Add(new JumpIfFalse(startIndex + sResult.Operations.Count + block.Operations.Count + 2)); 
                        sResult.LocalsUsed += block.LocalsUsed;
                        sResult.Operations.AddRange(block.Operations);
                    }

                    
                    var opsToReplace = new List<OpCode>();
                    if (ifNode.ElseNodes.Count > 0)
                    {
                        var endOfInitialBlock = new NoOp();
                        sResult.Operations.Add(endOfInitialBlock);
                        opsToReplace.Add(endOfInitialBlock);
                    }
                    
                    foreach (var eNode in ifNode.ElseNodes)
                    {
                        var block = CompileStatements(eNode.Block, definedSymbols, startIndex + sResult.Operations.Count);

                        if (eNode.Condition is not null)
                        {
                            var condRes = CompileExpression(eNode.Condition, definedSymbols);
                            sResult.Operations.AddRange(condRes.OpCodes);
                            sResult.Operations.Add(new JumpIfFalse(startIndex + sResult.Operations.Count + block.Operations.Count + 2));
                        }
                        
                        sResult.LocalsUsed += block.LocalsUsed;
                        sResult.Operations.AddRange(block.Operations);
                        var endOf = new NoOp();
                        sResult.Operations.Add(endOf);
                        opsToReplace.Add(endOf);
                    }

                    foreach (var index in opsToReplace.Select(opCode => sResult.Operations.IndexOf(opCode)))
                    {
                        sResult.Operations.RemoveAt(index);
                        
                        sResult.Operations.Insert(index, new Jump(startIndex + sResult.Operations.Count));
                    }
                    sResult.Operations.RemoveAt(sResult.Operations.Count - 1);
                    break;
                }
            }
        }

        return sResult;
    }

    private CompileExpressionResult CompileFunctionCall(FunctionCallExpressionNode callExpressionNode, List<Symbol> definedSymbols)
    {
        List<OpCode> operations = [];
        foreach (var argRes in callExpressionNode.Arguments.Select(arg => CompileExpression(arg, definedSymbols)))
        {
            operations.AddRange(argRes.OpCodes);
        }
        
        var res = CompileExpression(callExpressionNode.Function, definedSymbols);
        operations.AddRange(res.OpCodes);
        
        operations.Add(new Call());

        return new CompileExpressionResult(operations);
    }

    private CompileExpressionResult CompileExpression(ExpressionNode expressionNode, List<Symbol> definedSymbols)
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
                        operations =  [new LoadLocal(definedSymbols.IndexOf(symbol))];
                    }

                    if (_definedGlobalSymbols.Contains(symbol))
                    {
                        operations =  [new LoadGlobal(_definedGlobalSymbols.IndexOf(symbol))];
                        break;
                    }

                    if (symbol.Name == "exit")
                    {
                        operations = [new Exit()];
                        break;
                    }
                    
                    // undefined reference
                }
                break;
            }
            case FunctionNode functionNode:
            {
                List<Symbol> innerDefinedSymbols = [];
                foreach (var arg in functionNode.Arguments ?? [])
                {
                    innerDefinedSymbols.Add(_staticAnalysisResult.SymbolReferences[arg.Name]);
                }

                var res = CompileStatements(functionNode.Body, innerDefinedSymbols);

                var func = new RapidsBytecodeFunction([..res.Operations, new LoadBool(false), new Return()], (uint) (functionNode.Arguments?.Count ?? 0), res.LocalsUsed);
                _functions.Add(func);
                operations = [new LoadFunction(_functions.IndexOf(func))];
                
                break;
            }
            case LiteralNumberNode literalNumberNode:
            {
                operations = [new LoadNumber(literalNumberNode.Number)];
                break;
            }
            case BooleanNode booleanNode:
            {
                operations = [new LoadBool(booleanNode.Value.TokenType is TokenType.True)];
                break;
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
                            var res = CompileExpression(template.Value, definedSymbols);
                            operations.AddRange(res.OpCodes);
                            break;
                    }
                }

                if (stringNode.Parts.Count > 1)
                {
                    operations.Add(new Concat(stringNode.Parts.Count));
                }
                break;
            }
            case FunctionCallExpressionNode functionCallExpressionNode:
            {
                return CompileFunctionCall(functionCallExpressionNode, definedSymbols);
            }
        }

        return new CompileExpressionResult(operations);
    }

    private class CompileExpressionResult(List<OpCode> opCodes)
    {
        public List<OpCode> OpCodes { get; } = opCodes;
    }
}