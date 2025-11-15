using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RapidsLang.Analyzer;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.Parser.Types;
using RapidsLang.PreProcessor;

namespace RapidsLang.LanguageServer;

public class SemanticTokenVisitor
{
    private readonly SemanticTokensBuilder _builder;
    private readonly RapidsPreprocMetaData _metaData;
    private readonly string _code;
    private readonly RapidsStaticAnalysisResult? _staticAnalysisResult;
    private readonly Dictionary<Node, Node> _parentMap;
    
    private int _lastPushedSourceEndIndex = 0;
    private int _nextCommentIndex = 0;
    
    public SemanticTokenVisitor(SemanticTokensBuilder builder, RapidsPreprocMetaData metaData, string code, RapidsStaticAnalysisResult? staticAnalysisResult, Dictionary<Node, Node> parentMap)
    {
        _builder = builder;
        _metaData = metaData;
        _code = code;
        _staticAnalysisResult = staticAnalysisResult;
        _parentMap = parentMap;
    }

    public void Visit(Node node)
    {
        switch (node)
        {
            case StatementsNode statementsNode:
                foreach (var statement in statementsNode.Statements)
                {
                    Visit(statement);
                }
                break;
            case IfNode ifNode:
                PushToken(ifNode.BaseToken, SemanticTokenType.Keyword);
                Visit(ifNode.Condition);
                Visit(ifNode.Block);
                foreach (var elseNode in ifNode.ElseNodes)
                {
                    Visit(elseNode);
                }
                break;
            case WhileLoopNode whileLoopNode:
                PushToken(whileLoopNode.While, SemanticTokenType.Keyword);
                Visit(whileLoopNode.Condition);
                Visit(whileLoopNode.Block);
                break;
            case LiteralNumberNode numberNode:
                PushToken(numberNode.BaseToken, SemanticTokenType.Number);
                break;
            case OperationNode operationNode:
                Visit(operationNode.Left);
                PushToken(operationNode.Operator, SemanticTokenType.Operator);
                Visit(operationNode.Right);
                break;
            case ReturnNode returnNode:
                PushToken(returnNode.BaseToken, SemanticTokenType.Keyword);
                Visit(returnNode.Value);
                break;
            case ObjectNode objectNode:
                foreach (var (strNode, expressionNode) in objectNode.KeyValues)
                {
                    Visit(strNode);
                    Visit(expressionNode);
                }

                break;
            case StringNode stringNode:
                PushToken(stringNode.BaseToken, SemanticTokenType.String);
                foreach (var part in stringNode.Parts)
                {
                    switch (part)
                    {
                        case LiteralStringPart literalStringPart:
                            PushToken(literalStringPart.Value, SemanticTokenType.String);
                            break;
                        case TemplateStringPart templateStringPart:
                            PushToken(templateStringPart.OpenCurly, SemanticTokenType.Operator);
                            Visit(templateStringPart.Value);
                            PushToken(templateStringPart.ClosedCurly, SemanticTokenType.Operator);
                            break;
                    }
                }

                if (stringNode.EndString is not null)
                {
                    PushToken(stringNode.EndString, SemanticTokenType.String);
                }
                break;
            case DeclarationNode declarationNode:
                PushToken(declarationNode.BaseToken, SemanticTokenType.Keyword);
                var symbol = FindSymbol(declarationNode.Name.Value, declarationNode);
                
                var tokenType = SemanticTokenType.Variable;
                if (symbol?.Type is RapidsFunctionType)
                {
                    tokenType = SemanticTokenType.Function;
                }
                
                PushToken(declarationNode.Name, tokenType);
                Visit(declarationNode.Expression);
                break;
            case UseStatementNode useStatementNode:
                PushToken(useStatementNode.BaseToken, SemanticTokenType.Keyword);
                Visit(useStatementNode.ModuleName);
                break;
            case FunctionCallExpressionNode functionCallExpressionNode:
                Visit(functionCallExpressionNode.Function);
                foreach (var argument in functionCallExpressionNode.Arguments)
                {
                    Visit(argument);
                }
                break;
            case AssignmentNode assignmentNode:
                Visit(assignmentNode.Variable);
                PushToken(assignmentNode.Operator, SemanticTokenType.Operator);
                if(assignmentNode.Assignment is not null)
                    PushToken(assignmentNode.Assignment, SemanticTokenType.Operator);
                Visit(assignmentNode.Expression);
                break;
            case MemberAccessNode memberAccessNode:
                if (memberAccessNode.Left is not null)
                {
                    Visit(memberAccessNode.Left);
                }
                break;
            case ListItemAssignmentNode listItemAssignmentNode:
                Visit(listItemAssignmentNode.Array);
                Visit(listItemAssignmentNode.Index);
                PushToken(listItemAssignmentNode.Operator, SemanticTokenType.Operator);
                Visit(listItemAssignmentNode.Value);
                break;
            case FunctionCallStatementNode functionCallStatementNode:
                Visit(functionCallStatementNode.Function);
                break;
            case FunctionDeclarationNode functionDeclarationNode:
                PushToken(functionDeclarationNode.Name, SemanticTokenType.Function);
                Visit(functionDeclarationNode.Function);
                
                break;
            case FunctionNode functionNode:
                if (functionNode.Arguments is not null)
                {
                    foreach (var argument in functionNode.Arguments)
                    {
                        Visit(argument);
                    }
                }
                Visit(functionNode.Body);
                if (functionNode.DebugBody is not null)
                {
                    Visit(functionNode.DebugBody);
                }
                break;
            case ArgumentNode argumentNode:
                PushToken(argumentNode.Name, SemanticTokenType.Parameter);
                break;
            case BooleanNode booleanNode:
                PushToken(booleanNode.Value, SemanticTokenType.Number);
                break;
            case NullExpression nullExpression:
                PushToken(nullExpression.BaseToken, SemanticTokenType.Number);
                break;
            case LiteralModuleIdentifier literalModuleIdentifier:
                foreach (var tok in literalModuleIdentifier.Tokens)
                {
                    PushToken(tok, SemanticTokenType.String);
                }
                break;
            case StringModuleIdent stringModuleIdent:
                Visit(stringModuleIdent.StringNode);
                break;
            case ExportStatement exportStatement:
                PushToken(exportStatement.BaseToken, SemanticTokenType.Keyword);
                Visit(exportStatement.ExportNode);
                break;
            case FunctionExportable functionExportable:
                PushToken(functionExportable.BaseToken, SemanticTokenType.Function);
                Visit(functionExportable.FunctionNode);
                break;
            case ExpressionExportable expressionExportable:
                Visit(expressionExportable.Expression);
                break;
            case ChannelExportable targetOrSourceExportable:
                PushToken(targetOrSourceExportable.TargetOrSourceNode.BaseToken, SemanticTokenType.Keyword);
                if (targetOrSourceExportable.TargetOrSourceNode.DataName is not null)
                {
                    PushToken(targetOrSourceExportable.TargetOrSourceNode.DataName, SemanticTokenType.Parameter);
                }
                
                break;
            case ElseNode elseNode:
                PushToken(elseNode.BaseToken, SemanticTokenType.Keyword);
                if(elseNode.IfToken is not null)
                    PushToken(elseNode.IfToken, SemanticTokenType.Keyword);
                if (elseNode.Condition is not null)
                {
                    Visit(elseNode.Condition);
                }
                Visit(elseNode.Block);
                break;
            case BreakNode breakNode:
                PushToken(breakNode.BaseToken, SemanticTokenType.Keyword);
                break;
            case OnSourceStatement onSourceStatement:
                PushToken(onSourceStatement.BaseToken, SemanticTokenType.Keyword);
                Visit(onSourceStatement.Source);
                Visit(onSourceStatement.Body);
                break;
            case IdentifierNode identifierNode:
            {
                var idSymbol = FindSymbol(identifierNode.BaseToken.Value, identifierNode);

                var idType = SemanticTokenType.Variable;
                var modifiers = new List<SemanticTokenModifier>();
                if (idSymbol != null)
                {
                    if (idSymbol.Type is RapidsFunctionType)
                    {
                        idType = SemanticTokenType.Function;
                    }

                    if (idSymbol.IsConstant)
                    {
                        modifiers.Add(SemanticTokenModifier.Readonly);
                    }
                }

                PushToken(identifierNode.BaseToken, idType, modifiers);
                break;
            }
        }
    }
    
    private Symbol? FindSymbol(string name, Node contextNode)
    {
        if (_staticAnalysisResult == null) return null;

        var scopeBlock = contextNode.GetAncestor<StatementsNode>(_parentMap);
        
        if (scopeBlock == null && contextNode is StatementsNode rootNode)
        {
            scopeBlock = rootNode;
        }

        if (scopeBlock == null) return null;

        if (_staticAnalysisResult.Scopes.TryGetValue(scopeBlock, out var scope))
        {
            var currentScope = scope;
            while(currentScope != null)
            {
                var symbol = currentScope.Symbols.FirstOrDefault(s => s.Name == name);
                if (symbol != null)
                {
                    return symbol;
                }
                currentScope = currentScope.Parent;
            }
        }
        
        return null;
    }
    
    private void PushToken(Token token, SemanticTokenType tokenType, List<SemanticTokenModifier>? modifiers=null)
    {
        var typeIndex = RapidsSemanticTokensHandler.Legend.TokenTypes.ToList().IndexOf(tokenType);
        var modifierIndices =
            modifiers?.Select(m => RapidsSemanticTokensHandler.Legend.TokenModifiers.ToList().IndexOf(m)) ?? [];
        var modifierBits = modifierIndices.Aggregate(0, (current, index) => current | (1 << index));
        if (typeIndex == -1) return; 

        var currentSourceIndex = RapidsPreproc.GetSourceIdx(token.Index, _metaData);
        
        for (var i = _nextCommentIndex; i < _metaData.CommentedIndices.Count; i++)
        {
            var comment = _metaData.CommentedIndices[i];

            if (comment.SourceIndex < currentSourceIndex)
            {
                if (comment.SourceIndex >= _lastPushedSourceEndIndex)
                {
                    PushCommentToken(comment);
                    _lastPushedSourceEndIndex = comment.SourceIndex + comment.Length;
                    _nextCommentIndex = i + 1;
                }
            }
            else
            {
                break; 
            }
        }
        
        if (currentSourceIndex >= _lastPushedSourceEndIndex)
        {
            var (line, col) = RapidsPreproc.GetRowColFromIndex(currentSourceIndex, _code);

            _builder.Push(
                line - 1,
                col - 1,
                token.Value.Length,
                typeIndex,
                modifierBits
            );
            
            _lastPushedSourceEndIndex = currentSourceIndex + token.Value.Length;
        }
    }
    
    public void FlushRemainingComments()
    {
        for (var i = _nextCommentIndex; i < _metaData.CommentedIndices.Count; i++)
        {
            var comment = _metaData.CommentedIndices[i];
            if (comment.SourceIndex >= _lastPushedSourceEndIndex)
            {
                PushCommentToken(comment);
                _lastPushedSourceEndIndex = comment.SourceIndex + comment.Length;
            }
        }
    }
    
    private void PushCommentToken(CommentedIndices comment)
    {
        var typeIndex = RapidsSemanticTokensHandler.Legend.TokenTypes.ToList().IndexOf(SemanticTokenType.Comment);
        if (typeIndex == -1) return;

        var commentText = _code.Substring(comment.SourceIndex, comment.Length);

        var lines = commentText.Split('\n');

        var (startLine, startCol) = RapidsPreproc.GetRowColFromIndex(comment.SourceIndex, _code);

        for (var i = 0; i < lines.Length; i++)
        {
            var lineSegment = lines[i];

            var currentLine = startLine - 1 + i;
            int currentCol;
            int currentLength;

            if (i == 0)
            {
                currentCol = startCol - 1; 
                currentLength = lineSegment.Length;
            }
            else
            {
                var trimmedSegment = lineSegment.TrimStart();
                currentCol = lineSegment.Length - trimmedSegment.Length;
                currentLength = trimmedSegment.Length;
            }

            if (currentLength > 0)
                _builder.Push(
                    currentLine,
                    currentCol,
                    currentLength,
                    typeIndex,
                    0
                );
        }
    }
}