using RapidsLang.Extensions;
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
        => new($"For argument {argumentIndex + 1}, expected type {expected.Name} but got {actual.Name}", token.Index, token.EndIndex - token.Index, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfMayNotBeDefined(int index, int length, string variableName)
        => new($"Variable {variableName} may not be defined yet.", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfUnknownModule(int index, int length, string import)
        => new($"Module \"{import}\" is unknown.", index, length, RapidsStaticAnalysisSeverity.Warning);
    public static AnalysisDiagnostic OfNeverMutated(int index, int length, string variableName)
        => new($"Variable {variableName} was defined as a let, but was never mutated. Maybe use \"const\" instead to better convey the purpose?", index, length, RapidsStaticAnalysisSeverity.Warning);
    
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

public class Symbol(string name, bool isConstant, RapidsType? type=null, bool isArgument=false, int? startIndex=null)
{
    public string Name { get; } = name;
    public bool IsConstant { get; } = isConstant;
    public bool IsArgument { get; } = isArgument;
    public bool IsMutated { get; set; } = false;
    public RapidsType Type { get; set; } = type ?? RapidsAnyType.Instance;
    public int? StartIndex { get; } = startIndex;
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
    public List<Symbol> ExportedSymbols = [];
    // this one's going to be big.
    public Dictionary<ExpressionNode, RapidsType> ExpressionTypes { get; } = [];
    public Dictionary<IdentifierNode, Symbol> SymbolReferences { get; } = [];
}

public static class RapidsStaticAnalysis
{
    public static (RapidsParseResult ParseResult, RapidsPreprocMetaData MetaData, RapidsStaticAnalysisResult? analysis) Analyze(string code)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        RapidsStaticAnalysisResult? rapidsStaticAnalysisResult = null;

        if (parseResult.Diagnostics.Where(d => !d.IgnoreInLanguageServer).ToList().Count == 0)
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

        foreach (var symbol in scope.Symbols)
        {
            if(scope.Parent is not null && scope.Parent.Symbols.Contains(symbol))
            {
                continue;
            }

            if (symbol is { IsConstant: false, IsMutated: false })
            {
                result.Diagnostics.Add(AnalysisDiagnostic.OfNeverMutated(symbol.StartIndex!.Value, symbol.Name.Length, symbol.Name));
            }
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
                    else
                    {
                        symbol.IsMutated = true;
                    }

                    
                }
                break;
            case DeclarationNode declarationNode:
                var type = declarationNode.Type is not null ? ComputeFromTypeNode(declarationNode.Type) : GetType(declarationNode.Expression, scope, result);
                var declarationSymbol = new Symbol(
                    declarationNode.Name.Value,
                    declarationNode.Constant,
                    type,
                    startIndex: declarationNode.Name.Index
                );
                scope.Symbols.Add(declarationSymbol);
                break;
            case DefineTargetOrSourceStatement defineTargetOrSourceStatement:
                break;
            case UnfinishedMemberAccessNode unfinishedMemberAccessNode:
            {
                _ = GetType(unfinishedMemberAccessNode.Left, scope, result);
                break;
            }
            case ExportStatement exportStatement:
                Symbol? exportedSymbol = null;
                switch (exportStatement.ExportNode)
                {
                    case ChannelExportable channelExportable:
                        var otherChannel = scope.Symbols.Find(s => s.Name == channelExportable.TargetOrSourceNode.Name.Value);

                        if (otherChannel is null)
                        {
                            RapidsType channelType;
                            if (channelExportable.TargetOrSourceNode.IsTarget)
                            {
                                channelType =
                                    new RapidsChannelTargetType(
                                        ComputeFromTypeNode(channelExportable.TargetOrSourceNode.Type)
                                    );
                            }
                            else
                            {
                                channelType =
                                    new RapidsChannelSourceType(
                                        ComputeFromTypeNode(channelExportable.TargetOrSourceNode.Type),
                                        channelExportable.TargetOrSourceNode.DataName?.Value
                                    );
                            }

                            exportedSymbol = new Symbol(channelExportable.TargetOrSourceNode.Name.Value, true, channelType);
                        }
                        else
                        {
                            RapidsType channelType;
                            if (otherChannel.Type is RapidsChannelSourceType otherSourceType)
                            {
                                channelType = new RapidsBiDirectionalChannelType(
                                    otherSourceType,
                                    new RapidsChannelTargetType(
                                        ComputeFromTypeNode(channelExportable.TargetOrSourceNode.Type)
                                    )
                                );
                            }
                            else if (otherChannel.Type is RapidsChannelTargetType otherTargetType)
                            {
                                channelType = new RapidsBiDirectionalChannelType(
                                    new RapidsChannelSourceType(
                                        ComputeFromTypeNode(channelExportable.TargetOrSourceNode.Type),
                                        channelExportable.TargetOrSourceNode.DataName?.Value
                                    ),
                                    otherTargetType
                                );
                            }
                            else
                            {
                                channelType = RapidsAnyType.Instance;
                            }

                            exportedSymbol = new Symbol(
                                otherChannel.Name,
                                otherChannel.IsConstant,
                                channelType,
                                otherChannel.IsArgument,
                                otherChannel.StartIndex
                            );
                            scope.Symbols.Remove(otherChannel);
                            result.ExportedSymbols.Remove(otherChannel);
                        }
                        break;
                    case ExpressionExportable expressionExportable:
                        exportedSymbol = new Symbol(
                            expressionExportable.BaseToken.Value,
                            true,
                            GetType(expressionExportable.Expression, scope, result),
                            startIndex: expressionExportable.StartIndex
                        );
                        break;
                    case FunctionExportable functionExportable:
                        var funcExportType = GetType(functionExportable.FunctionNode, scope, result);
                        exportedSymbol = new(functionExportable.BaseToken.Value, true, funcExportType,
                            startIndex: exportStatement.StartIndex);
                        break;
                }

                if (exportedSymbol is null)
                {
                    break;
                }
                
                scope.Symbols.Add(exportedSymbol);
                result.ExportedSymbols.Add(exportedSymbol);
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
                // VisitStatements(functionDeclarationNode.Function.Body, scope.Child(BlockType.Function), result);
                _ = GetType(functionDeclarationNode.Function, scope, result);
                // if (functionDeclarationNode.Function.DebugBody is not null)
                // {
                //     VisitStatements(functionDeclarationNode.Function.DebugBody, scope.Child(BlockType.Function), result);
                // }
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
                var childScope = scope.Child(BlockType.SourceCallback);

                var sourceType = GetType(onSourceStatement.Source, scope, result);
                if (sourceType is not ( RapidsChannelSourceType or RapidsBiDirectionalChannelType) and not RapidsAnyType)
                {
                    result.Diagnostics.Add(AnalysisDiagnostic.OfOnStatementUsedOnNonSource(onSourceStatement.Source.BaseToken, sourceType));
                }

                if (sourceType is RapidsChannelSourceType { CallbackVariableName: not null } source)
                {
                    childScope.Symbols.Add(new Symbol(source.CallbackVariableName, true, source.ValueType));
                }else if (sourceType is RapidsBiDirectionalChannelType { Source: {CallbackVariableName: not null} biSource})
                {
                    childScope.Symbols.Add(new Symbol(biSource.CallbackVariableName, true, biSource.ValueType));
                }
                
                VisitStatements(onSourceStatement.Body, childScope, result);
                break;
            case PipeStatement pipeStatement:
                break;
            case UseStatementNode useStatementNode:
                var exported = ResolveExportedModuleTypes(useStatementNode.ModuleName, result);
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
                if (!scope.ParentScopeIncludes(BlockType.Loop) && !scope.ParentScopeIncludes(BlockType.SourceCallback))
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
        RapidsType computedType = RapidsAnyType.Instance;
        
        switch (expressionNode)
        {
            case BooleanNode:
                computedType = RapidsPrimitiveType.Bool;
                break;
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
                    computedType = returnType ?? RapidsAnyType.Instance;
                    break;
                }
                
                if (functionCallExpressionNode.Function is IdentifierNode)
                {
                    var value = GetType(fn, scope, result);

                    if (value is RapidsFunctionType functionType)
                    {
                        computedType = functionType.ReturnType ?? RapidsAnyType.Instance;
                        break;
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

                var childScope = scope.Child(BlockType.Function);

                foreach (var arg in functionNode.Arguments ?? [])
                {
                    childScope.Symbols.Add(new Symbol(arg.Name.Value, true, ComputeFromTypeNode(arg.Type), true));
                }
                
                VisitStatements(functionNode.Body, childScope, result);

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

                

                computedType = new RapidsFunctionType(argumentTypes, computedReturnType);
                break;
            case IdentifierNode identifierNode:
                var name = identifierNode.Token.Value;
                var firstInScope = scope.Symbols.FirstOrDefault(s => s.Name == name);
                
                if (firstInScope is not null)
                {
                    result.SymbolReferences[identifierNode] = firstInScope;
                    computedType = firstInScope.Type;
                    break;
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
                    computedType =  new RapidsArrayType(initialType);
                }
                break;
            case LiteralNumberNode:
                computedType =  RapidsPrimitiveType.Number;
                break;
            case MemberAccessNode memberAccessNode:
                var leftType = GetType(memberAccessNode.Left, scope, result);

                var memberType = leftType.GetMember(memberAccessNode.MemberName.Value);

                if (memberType is not null)
                {
                    computedType = memberType;
                    break;
                }
                
                
                result.Diagnostics.Add(AnalysisDiagnostic.OfMayNotBeDefined(
                    memberAccessNode.MemberName.Index,
                    memberAccessNode.MemberName.Value.Length,
                    memberAccessNode.MemberName.Value
                ));
                break;
            case NullExpression:
                computedType = RapidsPrimitiveType.Null;
                break;
            case ObjectNode objectNode:
            {
                if (objectNode.KeyValues.Count == 0)
                {
                    computedType = new RapidsDictionaryType(RapidsAnyType.Instance);
                    break;
                }
                var properties = new Dictionary<string, RapidsType>();

                foreach (var (keyNode, valueNode) in objectNode.KeyValues)
                {
                    var valueType = GetType(valueNode, scope, result);
                    
                    _ = GetType(keyNode, scope, result); 

                    properties[keyNode.BaseToken.Value] = valueType;
                }

                computedType = new RapidsShapeType(properties);
                break;
            }
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
                computedType = RapidsPrimitiveType.String;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expressionNode));
        }
        
        result.ExpressionTypes[expressionNode] = computedType;
        
        return computedType;
    }

    public static RapidsType ComputeFromTypeNode(TypeNode? typeNode)
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

    private static Dictionary<string, Symbol>? ResolveExportedModuleTypes(ModuleIdent ident, RapidsStaticAnalysisResult result)
    {
        var moduleName = ident.GetName();
        if (ModuleRegistry.NativeModules.ContainsKey(moduleName))
        {
            return ModuleRegistry.NativeModules.GetValueOrDefault(moduleName)?.Exports.Exports.Select(
                ex => (ex.Key, new Symbol(ex.Key, true, ex.Value.Type))
            ).ToDictionary();
        }
        
        var extensions = ExtensionLoader.GetExternalExtensions();

        var extension = extensions.Find(ex => ex.ExtensionManifest.ModuleName == moduleName);

        if (extension is not null)
        {
            var (parseResult, metaData, analysis) = Analyze(File.ReadAllText(extension.MainCodePath));
            if (analysis is null)
            {
                return [];
            }
            return analysis.ExportedSymbols.Select(ex => (ex.Name, ex)).ToDictionary();
        }
        
        result.Diagnostics.Add(AnalysisDiagnostic.OfUnknownModule(ident.StartIndex, moduleName.Length, moduleName));
        return [];
    }
}