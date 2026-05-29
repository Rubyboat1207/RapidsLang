using RapidsLang.Analyzer;
using RapidsLang.Interpreter;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.InterpreterVM;

public class BytecodeCompiler
{
    private readonly List<string> _strings = [];
    private readonly List<ModuleImport> _modules = [];
    private readonly List<OpCode> _operations = [];
    private RapidsStaticAnalysisResult _staticAnalysisResult = null!;

    public RapidProgram Compile(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        _staticAnalysisResult = staticAnalysisResult;
        CompileStatements(root);

        return new RapidProgram
        {
            Header = new BytecodeHeader
            {
                Version = 0,
                Strings = _strings.ToArray(),
                Modules = _modules.ToArray()
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
                    CompileFunctionCall(functionCall.Function);
                    break;
                }
            }
        }
    }

    private void CompileFunctionCall(FunctionCallExpressionNode callExpressionNode)
    {
        
    }

    private void CompileExpression(ExpressionNode expressionNode)
    {
        switch (expressionNode)
        {
            case IdentifierNode identifierNode:
            {
                if (_staticAnalysisResult.SymbolReferences.TryGetValue(identifierNode, out var symbol))
                {
                    
                }
                break;
            }
        }
    }
}