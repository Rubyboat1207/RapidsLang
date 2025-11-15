using RapidsLang.Analyzer;
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

        var symbol = FindSymbolAt(sourceIndex, analyzedDoc);
        if (symbol == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        var hoverContent = new MarkupContent
        {
            Kind = MarkupKind.Markdown,
            Value = $"```rapidslang\n(variable) {symbol.Name}: {symbol.Type.Name}\n```"
        };

        return Task.FromResult<Hover?>(new Hover { Contents = new MarkedStringsOrMarkupContent(hoverContent) });
    }

    private static Symbol? FindSymbolAt(int processedIndex, AnalyzedDocument analyzedDoc)
    {
        var parseResult = analyzedDoc.ParseResult;
        var parentMap = analyzedDoc.ParentMap;

        var node = parseResult.FindNodeAt(processedIndex);

        // ReSharper disable once InvertIf
        if (node is IdentifierNode identifier)
        {
            var owningBlock = node.GetAncestor<StatementsNode>(parentMap);

            if (owningBlock != null &&
                analyzedDoc.StaticAnalysisResult != null &&
                analyzedDoc.StaticAnalysisResult.Scopes.TryGetValue(owningBlock, out var scope))
            {
                return scope.Symbols.FirstOrDefault(s => s.Name == identifier.Token.Value);
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