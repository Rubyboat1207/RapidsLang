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
        
        var processedIndex = RapidsPreproc.GetProcessedIdx(sourceIndex, analyzedDoc.MetaData);

        var symbol = FindSymbolAt(processedIndex, analyzedDoc);
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

        if (node is null)
        {
            return null;
        }

        var name = "";
        if (node is IdentifierNode identifier)
        {
            name = identifier.Token.Value;
        }
        else if (node is MemberAccessNode memberAccessNode)
        {
            if (memberAccessNode.Left is null)
            {
                name = memberAccessNode.MemberName.Value;
            }
        }
        else if (node is ImportNode importNode)
        {
            name = importNode.AsName?.Value ?? importNode.BaseToken.Value;
        }else if (node is DeclarationNode declarationNode)
        {
            name = declarationNode.Name.Value;
        }

        var owningBlock = node.GetAncestor<StatementsNode>(parentMap);

        if (owningBlock != null &&
            analyzedDoc.StaticAnalysisResult != null &&
            analyzedDoc.StaticAnalysisResult.Scopes.TryGetValue(owningBlock, out var scope))
        {
            return scope.Symbols.FirstOrDefault(s => s.Name == name);
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