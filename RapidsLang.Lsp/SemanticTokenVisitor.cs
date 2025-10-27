using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;

namespace RapidsLang.LanguageServer;

public class SemanticTokenVisitor
{
    private readonly SemanticTokensBuilder _builder;
    private readonly RapidsPreprocMetaData _metaData;
    private readonly string _code;
    
    public SemanticTokenVisitor(SemanticTokensBuilder builder, RapidsPreprocMetaData metaData, string code)
    {
        _builder = builder;
        _metaData = metaData;
        _code = code;
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
                foreach (var elseNode in ifNode.ElseNodes)
                {
                    Visit(elseNode);
                }
                Visit(ifNode.Block);
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
            case StringNode stringNode:
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

                break;
            case DeclarationNode declarationNode:
                PushToken(declarationNode.BaseToken, SemanticTokenType.Keyword);
                PushToken(declarationNode.Name, SemanticTokenType.Variable);
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
            // case LiteralModuleIdentifier literalModuleIdentifier:
            //     foreach (var tok in literalModuleIdentifier.Tokens)
            //     {
            //         PushToken(tok, SemanticTokenType.String);
            //     }
            //     break;
            // case StringModuleIdent stringModuleIdent:
            //     Visit(stringModuleIdent.StringNode);
            //     break;
        }
    }
    
    private void PushToken(Token token, SemanticTokenType tokenType)
    {
        var typeIndex = RapidsSemanticTokensHandler.Legend.TokenTypes.ToList().IndexOf(tokenType);
        if (typeIndex == -1) 
        {
            return; 
        }

        var sourceIndex = RapidsPreproc.GetSourceIdx(token.Index, _metaData);
        var (line, col) = RapidsPreproc.GetRowColFromIndex(sourceIndex, _code);

        _builder.Push(
            line - 1,
            col - 1,
            token.Value.Length,
            typeIndex,
            0
        );
    }
}