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
        // 1. Get the cached analysis results.
        var analyzedDoc = _documentManager.GetDocument(request.TextDocument.Uri);
        
        // This logic is still the same. It only needs StaticAnalysisResult and Code.
        if (analyzedDoc?.StaticAnalysisResult == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        // 2. Convert LSP position (0-based) to your preprocessor's index
        // You'll still need to implement this helper
        var sourceIndex = RapidsPreproc.GetIndexFromRowCol(
            request.Position.Line + 1, 
            request.Position.Character + 1, 
            analyzedDoc.Code
        );

        // 3. Find the symbol at that index (This is the core logic for you to write)
        var symbol = FindSymbolAt(sourceIndex, analyzedDoc);
        if (symbol == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        // 4. Format the hover content
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