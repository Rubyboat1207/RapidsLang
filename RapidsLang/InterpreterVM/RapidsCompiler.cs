using System.Diagnostics.CodeAnalysis;
using RapidsLang.Analyzer;
using RapidsLang.Interpreter;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.InterpreterVM;

public class RapidsCompiler
{
    private readonly List<string> _strings = [];
    private readonly List<ModuleImport> _modules = [];
    private readonly List<OpCode> _operations = [];
    private readonly List<Symbol> _definedGlobalSymbols = [];
    private readonly List<Symbol> _definedSymbols = [];
    private RapidsStaticAnalysisResult _staticAnalysisResult = null!;

    public static RapidProgram Compile(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        return new RapidsCompiler().GenerateProgram(root, staticAnalysisResult);
    }
    
    public RapidProgram GenerateProgram(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        _staticAnalysisResult = staticAnalysisResult;
        CompileStatements(root);

        return new RapidProgram
        {
            Header = new BytecodeHeader
            {
                Version = 0,
                Strings = _strings.ToArray(),
                Modules = _modules.ToArray(),
                GlobalsCount = (uint) _definedGlobalSymbols.Count,
                OutermostLocalsCount = (uint) _definedSymbols.Count // for now. This must change with frames and such.
            },
            Code = _operations.ToArray()
        };
    }
    
    private void CompileStatements(StatementsNode root)
    {
        foreach (var statement in root.Statements)
        {
            switch (statement)
            {
                case FunctionCallStatementNode functionCall:
                {
                    _operations.AddRange(CompileFunctionCall(functionCall.Function));
                    break;
                }
                case DeclarationNode declarationNode:
                {
                    _operations.AddRange(CompileExpression(declarationNode.Expression));
                    _definedSymbols.Add(_staticAnalysisResult.SymbolReferences[declarationNode.Name]);
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
            }
        }
    }

    private List<OpCode> CompileFunctionCall(FunctionCallExpressionNode callExpressionNode)
    {
        List<OpCode> operations = [];
        foreach (var arg in callExpressionNode.Arguments)
        {
            operations.AddRange(CompileExpression(arg));
        }
        operations.AddRange(CompileExpression(callExpressionNode.Function));
        operations.Add(new Call());

        return operations;
    }

    private List<OpCode> CompileExpression(ExpressionNode expressionNode)
    {
        List<OpCode> operations = [];
        switch (expressionNode)
        {
            case IdentifierNode identifierNode:
            {
                if (_staticAnalysisResult.SymbolReferences.TryGetValue(identifierNode, out var symbol))
                {
                    if (_definedSymbols.Contains(symbol))
                    {
                        return [new LoadLocal(_definedSymbols.IndexOf(symbol))];
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
                            operations.AddRange(CompileExpression(template.Value));
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