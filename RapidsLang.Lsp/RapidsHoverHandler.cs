using RapidsLang.Analyzer;
using RapidsLang.Analyzer.Types;
using RapidsLang.Parser.Nodes;
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

        var type = GetTypeAt(processedIndex, analyzedDoc, out var name);
        if (type == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        if (name is null)
        {
            return Task.FromResult<Hover?>(new Hover()
            {
                Contents = new(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"```rapidslang\n(variable) {type.Name}\n```"
                })
            });
        }
        else
        {
            return Task.FromResult<Hover?>(new Hover()
            {
                Contents = new(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"```rapidslang\n(variable) {name} {type.Name}\n```"
                })
            });
        }
        
    }
    
    public static RapidsType? GetTypeAt(int processedIndex, AnalyzedDocument analyzedDoc, out string? symbolName)
    {
        var parseResult = analyzedDoc.ParseResult;
        var parentMap = analyzedDoc.ParentMap;
        var analysisResult = analyzedDoc.StaticAnalysisResult;

        symbolName = null;
        if (analysisResult == null) return null;

        var node = parseResult.FindNodeAt(processedIndex);
        if (node == null) return null;
        
        Node nodeToLookUp = node;

        if (node is IdentifierNode identifier && parentMap.TryGetValue(identifier, out var parent))
        {
            symbolName = identifier.Token.Value;
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

        symbolName = null;
        Node? scopeSearchNode = node;
        
        if (node is DeclarationNode decl)
        {
            if (processedIndex >= decl.Name.BaseToken.Index && processedIndex <= decl.Name.BaseToken.Index + decl.Name.Value.Length)
            {
                symbolName = decl.Name.Value;
            }
        }
        else if (node is ImportNode import)
        {
            if (import.AsName != null &&
                processedIndex >= import.AsName.Index && 
                processedIndex <= import.AsName.Index + import.AsName.Value.Length)
            {
                symbolName = import.AsName.Value;
            }
            else if (import.AsName == null && 
                processedIndex >= import.BaseToken.Index && 
                processedIndex <= import.BaseToken.Index + import.BaseToken.Value.Length)
            {
                symbolName = import.BaseToken.Value;
            }
        }
        else if (node is IdentifierNode idNode && parentMap.TryGetValue(idNode, out var idParent))
        {
            if (idParent is DeclarationNode parentDecl && parentDecl.Name.Value == idNode.Token.Value)
            {
                symbolName = idNode.Token.Value;
            }
            else if (idParent is ImportNode parentImport && parentImport.AsName?.Value == idNode.Token.Value)
            {
                symbolName = idNode.Token.Value;
            }
            // todo: Add 'else if' for FunctionArgumentNode, etc.
        }


        if (symbolName != null)
        {
            var owningBlock = scopeSearchNode.GetAncestor<StatementsNode>(parentMap);
            if (owningBlock != null &&
                analysisResult.Scopes.TryGetValue(owningBlock, out var scope))
            {
                var localSymbolName = symbolName;
                var symbol = scope.Symbols.FirstOrDefault(s => s.Name == localSymbolName);
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