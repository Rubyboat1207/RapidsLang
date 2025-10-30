using RapidsLang.Interpreter;
using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;
using RapidsLang.Parser.Types;
using RapidsLang.PreProcessor;

namespace RapidsLang.Analyzer;

public enum RapidsStaticAnalysisSeverity
{
    Hint,
    Warning,
    Error
}

public class AnalysisDiagnostic(string message, int index, int length, RapidsStaticAnalysisSeverity severity)
{
    public string Message { get; } = message;
    public int Index { get; } = index;
    public int Length { get; } = length;
    public RapidsStaticAnalysisSeverity Severity { get; } = severity;
    
    // ----- WARNINGS ----- //
    public static AnalysisDiagnostic ofUnreachableCode(int index, int length)
        => new("This block of code is unreachable", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic ofMayNotBeDefined(int index, int length, string variableName)
        => new($"Variable {variableName} may not be defined yet.", index, length, RapidsStaticAnalysisSeverity.Warning);
    
    // ------ ERRORS ------ //
    public static AnalysisDiagnostic ofConstantModified(int index, int length, string variableName)
        => new($"Constant {variableName} was modified.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic ofInvalidBreak(int index, int length)
        => new($"Break was used in a non loop context.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic ofInvalidContinue(int index, int length)
        => new($"Continue was used in a non loop context.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic ofInvalidReturn(int index, int length)
        => new($"Return was used in a non function context.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic ofMustReturnHintedType(int index, int length, string? name, string expectedType, string actualType)
        => new($"Function {name ?? "anonymous"} must return {expectedType}. Actually returns {actualType}", index, length, RapidsStaticAnalysisSeverity.Error);
}

public class Symbol(string name, bool isConstant, RapidsType? type=null)
{
    public string Name { get; } = name;
    public bool IsConstant { get; } = isConstant;
    public bool IsMutated { get; set; } = false;
    public RapidsType Type { get; set; } = type ?? RapidsAnyType.Instance;
}

public class RapidsStaticAnalysisScope
{
    public RapidsStaticAnalysisScope? Parent;
    
    public List<Symbol> Symbols = [];
    public List<RapidsType> Aliases = [];

    public BlockType BlockType;

    public RapidsType? ReturnValue = null;

    public RapidsStaticAnalysisScope Child(BlockType type)
    {
        return new RapidsStaticAnalysisScope()
        {
            Parent = this,
            Symbols = [..Symbols],
            Aliases = [..Aliases],
            BlockType = type,
        };
    }

    public bool ParentScopeIncludes(BlockType blockType)
    {
        if (BlockType == blockType)
        {
            return true;
        }
        
        return Parent != null && Parent.ParentScopeIncludes(blockType);
    }
}

public class RapidsStaticAnalysisResult
{
    public List<AnalysisDiagnostic> Diagnostics { get; } = [];
    public Dictionary<StatementsNode, RapidsStaticAnalysisScope> Scopes = [];
}

public static class RapidsStaticAnalysis
{
    public static (RapidsParseResult ParseResult, RapidsPreprocMetaData MetaData, RapidsStaticAnalysisResult? analysis) Analyze(string code)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        RapidsStaticAnalysisResult? rapidsStaticAnalysisResult = null;

        if (parseResult.Diagnostics.Count == 0)
        {
            rapidsStaticAnalysisResult = StaticAnalysis(preprocRes.Metadata, parseResult.RootNode);
        }

        return (parseResult, preprocRes.Metadata, rapidsStaticAnalysisResult);
    }

    public static RapidsStaticAnalysisResult StaticAnalysis(RapidsPreprocMetaData metaData, StatementsNode rootNode)
    {
        var result = new RapidsStaticAnalysisResult();
        
        var globalScope = new RapidsStaticAnalysisScope
        {
            Parent = null,
            Symbols = InterpreterContext.GlobalSymbols.Select((kvp) => new Symbol(
                kvp.Key,
                true,
                kvp.Value.Item2
            )).ToList(),
            BlockType = BlockType.Module
        };
        
        result.Scopes[rootNode] = globalScope;
        
        VisitStatements(rootNode, globalScope, result);

        return result;
    }

    public static void VisitStatements(
        StatementsNode node,
        RapidsStaticAnalysisScope scope,
        RapidsStaticAnalysisResult result
    )
    {
        result.Scopes[node] = scope;
        
        foreach (var statement in node.Statements)
        {
            VisitStatement(statement, scope, result);
        }
    }

    public static void VisitStatement(
        StatementNode node,
        RapidsStaticAnalysisScope scope,
        RapidsStaticAnalysisResult result
    )
    {
        switch (node)
        {
            case AssignmentNode assignmentNode:
                if (assignmentNode.Variable.Left is null )
                {
                    var name = assignmentNode.Variable.MemberName;
                    var symbol = scope.Symbols.FirstOrDefault(s => s.Name == name.Value);

                    if (symbol is null)
                    {
                        result.Diagnostics.Add(AnalysisDiagnostic.ofMayNotBeDefined(name.Index, name.Value.Length, name.Value));
                    }
                    else if(symbol.IsConstant)
                    {
                        result.Diagnostics.Add(AnalysisDiagnostic.ofConstantModified(name.Index, name.Value.Length, name.Value));
                    }
                }
                
                
                
                break;
            case DeclarationNode declarationNode:
                scope.Symbols.Add(new Symbol(declarationNode.Name.Value, declarationNode.Constant, ComputeFromTypeNode(declarationNode.Type)));
                break;
            case DefineTargetOrSourceStatement defineTargetOrSourceStatement:
                break;
            case ExportStatement exportStatement:
                break;
            case FunctionCallStatementNode functionCallStatementNode:
                break;
            case FunctionDeclarationNode functionDeclarationNode:
                break;
            case IfNode ifNode:
                break;
            case ListItemAssignmentNode listItemAssignmentNode:
                break;
            case OnSourceStatement onSourceStatement:
                break;
            case PipeStatement pipeStatement:
                break;
            case UseStatementNode useStatementNode:
                break;
            case WhileLoopNode whileLoopNode:
                break;
            case ReturnNode returnNode:
                if (!scope.ParentScopeIncludes(BlockType.Function))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.ofInvalidReturn(returnNode.BaseToken.Index, returnNode.BaseToken.Value.Length));
                }
                break;
            case BreakNode breakNode:
                if (!scope.ParentScopeIncludes(BlockType.Loop))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.ofInvalidBreak(breakNode.BaseToken.Index, breakNode.BaseToken.Value.Length));
                }
                break;
            case ContinueNode continueNode:
                if (!scope.ParentScopeIncludes(BlockType.Loop))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.ofInvalidContinue(continueNode.BaseToken.Index, continueNode.BaseToken.Value.Length));
                }
                break;
        }
    }

    public static RapidsType GetType(ExpressionNode expressionNode, RapidsStaticAnalysisScope scope, RapidsStaticAnalysisResult result)
    {
        switch (expressionNode)
        {
            case BooleanNode:
                return RapidsPrimitiveType.Bool;
            case FunctionCallExpressionNode functionCallExpressionNode:
                var fn = functionCallExpressionNode.Function;
                if (functionCallExpressionNode.Function is FunctionNode fnNode)
                {
                    RapidsType? returnType;
                    if (result.Scopes.TryGetValue(fnNode.Body, out var value))
                    {
                        returnType = value.ReturnValue;
                    }
                    else
                    {
                        returnType = ((RapidsFunctionType) GetType(functionCallExpressionNode.Function, scope, result)).ReturnType;
                    }

                    return returnType ?? RapidsAnyType.Instance;
                }

                if (functionCallExpressionNode.Function is IdentifierNode)
                {
                    return GetType(fn, scope, result);
                }
                
                // todo: continue this
                break;
            case FunctionNode functionNode:
                var argumentTypes = new List<RapidsType>();
                if (functionNode.Arguments != null)
                    foreach (var arg in functionNode.Arguments)
                    {
                        if (arg.Type is not null)
                        {
                            argumentTypes.Add(ComputeFromTypeNode(arg.Type));
                        }
                        else
                        {
                            argumentTypes.Add(RapidsAnyType.Instance);
                        }
                    }

                var statedReturnType = functionNode.ReturnType is null ? null : ComputeFromTypeNode(functionNode.ReturnType);
                
                VisitStatements(functionNode.Body, scope.Child(BlockType.Function), result);

                var computedReturnType = result.Scopes[functionNode.Body].ReturnValue;

                if (statedReturnType is not null && computedReturnType is not null)
                {
                    if (!statedReturnType.IsSameType(computedReturnType))
                    {
                        result.Diagnostics.Add(AnalysisDiagnostic.ofMustReturnHintedType(
                            functionNode.BaseToken.Index,
                            functionNode.BaseToken.Value.Length,
                            null,
                            statedReturnType.Name,
                            computedReturnType.Name
                        ));
                    }
                } else if (statedReturnType is not null && computedReturnType is null)
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.ofMustReturnHintedType(
                        functionNode.BaseToken.Index,
                        functionNode.BaseToken.Value.Length,
                        null,
                        statedReturnType.Name,
                        "void"
                    ));
                }else if (statedReturnType is null && computedReturnType is not null)
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.ofMustReturnHintedType(
                        functionNode.BaseToken.Index,
                        functionNode.BaseToken.Value.Length,
                        null,
                        "void",
                        computedReturnType.Name
                    ));
                }

                

                return new RapidsFunctionType(argumentTypes, computedReturnType);
            case IdentifierNode identifierNode:
                var name = identifierNode.Token.Value;
                var firstInScope = scope.Symbols.FirstOrDefault(s => s.Name == name);
                
                if (firstInScope is not null)
                {
                    return firstInScope.Type;
                }
                
                result.Diagnostics.Add(AnalysisDiagnostic.ofMayNotBeDefined(identifierNode.Token.Index, name.Length, name));
                break;
            case ListNode listNode:
                break;
            case LiteralNumberNode:
                return RapidsPrimitiveType.Number;
            case MemberAccessNode memberAccessNode:
                break;
            case NullExpression:
                return RapidsPrimitiveType.Null;
            case ObjectNode objectNode:
                break;
            case OperationNode operationNode:
                break;
            case StringNode:
                return RapidsPrimitiveType.String;
            default:
                throw new ArgumentOutOfRangeException(nameof(expressionNode));
        }

        return RapidsAnyType.Instance;
    }

    private static RapidsType ComputeFromTypeNode(TypeNode? typeNode)
    {
        if (typeNode == null)
        {
            return RapidsAnyType.Instance;
        }

        RapidsType baseType;

        switch (typeNode)
        {
            case IdentifierTypeNode identNode:
                baseType = identNode.Identifier.Value switch
                {
                    "string"  => RapidsPrimitiveType.String,
                    "number"  => RapidsPrimitiveType.Number,
                    "bool"    => RapidsPrimitiveType.Bool,
                    "void"    => RapidsPrimitiveType.Null,
                    "null"    => RapidsPrimitiveType.Null,
                    _         => RapidsAnyType.Instance 
                };
                break;

            case ObjectTypeNode objNode:
                var properties = new Dictionary<string, RapidsType>();
                foreach (var prop in objNode.Properties)
                {
                    RapidsType propType = ComputeFromTypeNode(prop.Type);
                    properties[prop.Name.Value] = propType;
                }
                baseType = new RapidsShapeType(properties);
                break;

            default:
                baseType = RapidsAnyType.Instance;
                break;
        }
        
        if (typeNode.Optional)
        {
            if (baseType == RapidsPrimitiveType.Null)
            {
                return baseType;
            }

            return new RapidsUnionType([
                baseType,
                RapidsPrimitiveType.Null
            ]);
        }

        return baseType;
    }
}