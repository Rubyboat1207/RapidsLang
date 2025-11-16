using RapidsLang.Analyzer;
using RapidsLang.Parser.Nodes;
using RapidsLang.Parser.Types;
using RapidsLang.PreProcessor;

namespace RapidsLang.LanguageServer;

using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

public class RapidsHoverHandler : IHoverHandler
{
    private readonly DocumentManager _documentManager; // Assumes DocumentManager stores AnalyzedDocument
    private readonly TextDocumentSelector _documentSelector = new(
        new TextDocumentFilter { Language = "rapidslang" }
    );

    public RapidsHoverHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var analyzedDoc = _documentManager.GetDocument(request.TextDocument.Uri);
        
        if (analyzedDoc?.StaticAnalysisResult == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        var sourceIndex = RapidsPreproc.GetIndexFromRowCol(
            request.Position.Line + 1,
            request.Position.Character + 1,
            analyzedDoc.Code
        );
        
        var processedIndex = RapidsPreproc.GetProcessedIdx(sourceIndex, analyzedDoc.MetaData);

        var symbol = GetTypeAt(processedIndex, analyzedDoc);
        if (symbol == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        var hoverContent = new MarkupContent
        {
            Kind = MarkupKind.Markdown,
            Value = $"```rapidslang\n(variable) {symbol.Name}\n```"
        };

        return Task.FromResult<Hover?>(new Hover { Contents = new MarkedStringsOrMarkupContent(hoverContent) });
    }
    
    public static RapidsType? GetTypeAt(int processedIndex, AnalyzedDocument analyzedDoc)
    {
        var parseResult = analyzedDoc.ParseResult;
        var parentMap = analyzedDoc.ParentMap;
        var analysisResult = analyzedDoc.StaticAnalysisResult;

        if (analysisResult == null) return null;

        var node = parseResult.FindNodeAt(processedIndex);
        if (node == null) return null;
        
        Node nodeToLookUp = node;

        if (node is IdentifierNode identifier && parentMap.TryGetValue(identifier, out var parent))
        {
            if (parent is MemberAccessNode memberAccess && memberAccess.MemberName.Value == identifier.Token.Value)
            {
                nodeToLookUp = parent;
            }
        }

        if (nodeToLookUp is ExpressionNode expressionNode)
        {
            if (analysisResult.ExpressionTypes.TryGetValue(expressionNode, out var type))
            {
                return type;
            }
        }

        string? name = null;
        Node? scopeSearchNode = node;
        
        if (node is DeclarationNode decl)
        {
            if (processedIndex >= decl.Name.Index && processedIndex <= decl.Name.Index + decl.Name.Value.Length)
            {
                name = decl.Name.Value;
            }
        }
        else if (node is ImportNode import)
        {
            if (import.AsName != null &&
                processedIndex >= import.AsName.Index && 
                processedIndex <= import.AsName.Index + import.AsName.Value.Length)
            {
                name = import.AsName.Value;
            }
            else if (import.AsName == null && 
                processedIndex >= import.BaseToken.Index && 
                processedIndex <= import.BaseToken.Index + import.BaseToken.Value.Length)
            {
                name = import.BaseToken.Value;
            }
        }
        else if (node is IdentifierNode idNode && parentMap.TryGetValue(idNode, out var idParent))
        {
            if (idParent is DeclarationNode parentDecl && parentDecl.Name.Value == idNode.Token.Value)
            {
                name = idNode.Token.Value;
            }
            else if (idParent is ImportNode parentImport && parentImport.AsName?.Value == idNode.Token.Value)
            {
                name = idNode.Token.Value;
            }
            // todo: Add 'else if' for FunctionArgumentNode, etc.
        }


        if (name != null)
        {
            var owningBlock = scopeSearchNode.GetAncestor<StatementsNode>(parentMap);
            if (owningBlock != null &&
                analysisResult.Scopes.TryGetValue(owningBlock, out var scope))
            {
                var symbol = scope.Symbols.FirstOrDefault(s => s.Name == name);
                if (symbol != null)
                {
                    return symbol.Type;
                }
            }
        }

        return null;
    }

    public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = _documentSelector
        };
    }
}