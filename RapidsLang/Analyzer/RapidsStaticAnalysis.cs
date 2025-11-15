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
    public static AnalysisDiagnostic OfUnreachableCode(int index, int length)
        => new("This block of code is unreachable", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfCalledNonFunction(int index, int length)
        => new("Attempted to call non function", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfIncorrectAmountOfArguments(int index, int length)
        => new("Attempted to call function with incorrect amount of arguments", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfArgumentTypeIncorrect(Token token, int argumentIndex, RapidsType expected, RapidsType actual)
        => new($"For argument {argumentIndex + 1}, expected type {expected} but got {actual}", token.Index, token.EndIndex - token.Index, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfMayNotBeDefined(int index, int length, string variableName)
        => new($"Variable {variableName} may not be defined yet.", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfUnknownModule(int index, int length, string import)
        => new($"Module \"{import}\" is unknown.", index, length, RapidsStaticAnalysisSeverity.Warning);
    
    // ------ ERRORS ------ //
    public static AnalysisDiagnostic OfConstantModified(int index, int length, string variableName)
        => new($"Constant {variableName} was modified.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic OfInvalidBreak(int index, int length)
        => new($"Break was used in a non loop context.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic OfInvalidContinue(int index, int length)
        => new($"Continue was used in a non loop context.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic OfInvalidReturn(int index, int length)
        => new($"Return was used in a non function context.", index, length, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic OfOnStatementUsedOnNonSource(Token token, RapidsType actual)
        => new($"On statement's source was not a source, was {actual.Name}", token.Index, token.EndIndex - token.Index, RapidsStaticAnalysisSeverity.Error);
    public static AnalysisDiagnostic OfMustReturnHintedType(int index, int length, string? name, string expectedType, string actualType)
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
                _ = GetType(assignmentNode.Variable.Left, scope, result);
                if (assignmentNode.Variable.Left is null)
                {
                    var name = assignmentNode.Variable.MemberName;
                    var symbol = scope.Symbols.FirstOrDefault(s => s.Name == name.Value);

                    if (symbol is null)
                    {
                        result.Diagnostics.Add(AnalysisDiagnostic.OfMayNotBeDefined(name.Index, name.Value.Length, name.Value));
                    }
                    else if(symbol.IsConstant)
                    {
                        result.Diagnostics.Add(AnalysisDiagnostic.OfConstantModified(name.Index, name.Value.Length, name.Value));
                    }
                }
                break;
            case DeclarationNode declarationNode:
                var type = declarationNode.Type is not null ? ComputeFromTypeNode(declarationNode.Type) : GetType(declarationNode.Expression, scope, result);
                scope.Symbols.Add(new Symbol(declarationNode.Name.Value, declarationNode.Constant, type));
                break;
            case DefineTargetOrSourceStatement defineTargetOrSourceStatement:
                break;
            case ExportStatement exportStatement:
                break;
            case FunctionCallStatementNode functionCallStatementNode:
                var functionType = GetType(functionCallStatementNode.Function.Function, scope, result);

                if (!functionType.IsSameType(RapidsFunctionType.AnyFunctionType))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfCalledNonFunction(functionCallStatementNode.BaseToken.Index, functionCallStatementNode.BaseToken.Value.Length));
                }

                var func = functionType is RapidsFunctionType rapidsFuncType ? rapidsFuncType : null;
                
                if (func is not null)
                {
                    if (func.ParameterTypes.Count != functionCallStatementNode.Function.Arguments.Count)
                    {
                        Node firstArgument = functionCallStatementNode.Function.Arguments.FirstOrDefault() ?? functionCallStatementNode.Function.Function;
                        Node lastArgument = functionCallStatementNode.Function.Arguments.LastOrDefault() ?? firstArgument;
                        
                        result.Diagnostics.Add(AnalysisDiagnostic.OfIncorrectAmountOfArguments(firstArgument.BaseToken.Index, lastArgument.BaseToken.EndIndex - firstArgument.BaseToken.Index));
                    }
                }

                for (var i = 0; i < functionCallStatementNode.Function.Arguments.Count; i++)
                {
                    var callArgExpr = functionCallStatementNode.Function.Arguments[i];
                    var callArgType = GetType(callArgExpr, scope, result);
                    var signatureArg = func?.ParameterTypes.ElementAtOrDefault(i);

                    if (signatureArg == null)
                    {
                        continue;
                    }

                    if (callArgType.IsSameType(signatureArg))
                    {
                        continue;
                    }

                    result.Diagnostics.Add(AnalysisDiagnostic.OfArgumentTypeIncorrect(
                        callArgExpr.BaseToken,
                        i,
                        signatureArg,
                        callArgType
                    ));
                }
                break;
            case FunctionDeclarationNode functionDeclarationNode:
                scope.Symbols.Add(new(functionDeclarationNode.Name.Value, true, GetType(functionDeclarationNode.Function, scope, result)));
                VisitStatements(functionDeclarationNode.Function.Body, scope.Child(BlockType.Function), result);
                if (functionDeclarationNode.Function.DebugBody is not null)
                {
                    VisitStatements(functionDeclarationNode.Function.DebugBody, scope.Child(BlockType.Function), result);
                }
                break;
            case IfNode ifNode:
                VisitStatements(ifNode.Block, scope.Child(BlockType.Statement), result);
                _ = GetType(ifNode.Condition, scope, result);
                foreach (var el in ifNode.ElseNodes)
                {
                    if (el.Condition is not null)
                    {
                        _ = GetType(el.Condition, scope, result);
                    }
                    VisitStatements(el.Block, scope.Child(BlockType.Statement), result);
                }
                break;
            case ListItemAssignmentNode listItemAssignmentNode:
                break;
            case OnSourceStatement onSourceStatement:
                _ = GetType(onSourceStatement.Source, scope, result);
                var childScope = scope.Child(BlockType.SourceCallback);

                var sourceType = GetType(onSourceStatement.Source, scope, result);
                if (sourceType is not RapidsChannelSourceType && sourceType is not RapidsAnyType)
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfOnStatementUsedOnNonSource(onSourceStatement.Source.BaseToken, sourceType));
                }

                if (sourceType is RapidsChannelSourceType { CallbackVariableName: not null } source)
                {
                    childScope.Symbols.Add(new Symbol(source.CallbackVariableName, true, source.ValueType));
                }
                
                VisitStatements(onSourceStatement.Body, childScope, result);
                break;
            case PipeStatement pipeStatement:
                break;
            case UseStatementNode useStatementNode:
                var exported = ResolveExportedModuleTypes(useStatementNode.ModuleName.GetName());
                if (exported is null)
                {
                    break;
                }

                if (useStatementNode.ImportNodes is not null)
                {
                    foreach (var importNode in useStatementNode.ImportNodes)
                    {
                        if (exported.TryGetValue(importNode.BaseToken.Value, out var value))
                        {
                            scope.Symbols.Add(importNode.AsName is null
                                ? value
                                : new Symbol(importNode.AsName.Value, value.IsConstant, value.Type));
                        }
                    }
                }
                else
                {
                    exported.Select(ex => ex.Value).ToList().ForEach(scope.Symbols.Add);
                }
                break;
            case WhileLoopNode whileLoopNode:
                _ = GetType(whileLoopNode.Condition, scope, result);
                VisitStatements(whileLoopNode.Block, scope.Child(BlockType.Loop), result);
                break;
            case ReturnNode returnNode:
                if (!scope.ParentScopeIncludes(BlockType.Function))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfInvalidReturn(returnNode.BaseToken.Index, returnNode.BaseToken.Value.Length));
                }
                break;
            case BreakNode breakNode:
                if (!scope.ParentScopeIncludes(BlockType.Loop))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfInvalidBreak(breakNode.BaseToken.Index, breakNode.BaseToken.Value.Length));
                }
                break;
            case ContinueNode continueNode:
                if (!scope.ParentScopeIncludes(BlockType.Loop))
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfInvalidContinue(continueNode.BaseToken.Index, continueNode.BaseToken.Value.Length));
                }
                break;
        }
    }

    public static RapidsType GetType(ExpressionNode? expressionNode, RapidsStaticAnalysisScope scope, RapidsStaticAnalysisResult result)
    {
        if (expressionNode is null)
        {
            return RapidsAnyType.Instance;
        }
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
                    // todo: add warning for assigning void to variable
                    return returnType ?? RapidsAnyType.Instance;
                }
                
                if (functionCallExpressionNode.Function is IdentifierNode)
                {
                    var value = GetType(fn, scope, result);

                    if (value is RapidsFunctionType functionType)
                    {
                        return functionType.ReturnType ?? RapidsAnyType.Instance;
                    }
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
                        result.Diagnostics.Add(AnalysisDiagnostic.OfMustReturnHintedType(
                            functionNode.BaseToken.Index,
                            functionNode.BaseToken.Value.Length,
                            null,
                            statedReturnType.Name,
                            computedReturnType.Name
                        ));
                    }
                } else if (statedReturnType is not null && computedReturnType is null)
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfMustReturnHintedType(
                        functionNode.BaseToken.Index,
                        functionNode.BaseToken.Value.Length,
                        null,
                        statedReturnType.Name,
                        "void"
                    ));
                }else if (statedReturnType is null && computedReturnType is not null)
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfMustReturnHintedType(
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
                
                result.Diagnostics.Add(AnalysisDiagnostic.OfMayNotBeDefined(identifierNode.Token.Index, name.Length, name));
                break;
            case ListNode listNode:
                RapidsType? initialType = null;
                bool isSameType = true;
                foreach (var element in listNode.Values)
                {
                    if (initialType is null)
                    {
                        initialType = GetType(element, scope, result);
                        continue;
                    }
                    
                    var type = GetType(element, scope, result);
                    if (!initialType.IsSameType(type))
                    {
                        isSameType = false;
                    }
                }

                if (isSameType && initialType is not null)
                {
                    return new RapidsArrayType(initialType);
                }
                break;
            case LiteralNumberNode:
                return RapidsPrimitiveType.Number;
            case MemberAccessNode memberAccessNode:
                _ = GetType(memberAccessNode.Left, scope, result);
                break;
            case NullExpression:
                return RapidsPrimitiveType.Null;
            case ObjectNode objectNode:
                break;
            case OperationNode operationNode:
                _ = GetType(operationNode.Left, scope, result);
                _ = GetType(operationNode.Right, scope, result);
                break;
            case StringNode stringNode:
                foreach (var stringNodePart in stringNode.Parts)
                {
                    if (stringNodePart is TemplateStringPart templateStringPart)
                    {
                        _ = GetType(templateStringPart.Value, scope, result);
                    }
                }
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

    private static Dictionary<string, Symbol>? ResolveExportedModuleTypes(string moduleName)
    {
        // im only supporting native modules for now.

        return ModuleRegistry.NativeModules.GetValueOrDefault(moduleName)?.Exports.Exports.Select(
            ex => (ex.Key, new Symbol(ex.Key, true, ex.Value.Type))
        ).ToDictionary();
    }
}