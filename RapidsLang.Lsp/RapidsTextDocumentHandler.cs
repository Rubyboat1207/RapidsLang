using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using RapidsLang.Analyzer;
using RapidsLang.PreProcessor;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RapidsLang.LanguageServer;

public class RapidsTextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly ILanguageServerFacade _facade;
    private readonly DocumentManager _documentManager;
    
    private const string LanguageId = "rapidslang";
    
    public RapidsTextDocumentHandler(ILanguageServerFacade facade, DocumentManager documentManager)
    {
        _facade = facade;
        _documentManager = documentManager;
    }
    
    private readonly TextDocumentSelector _documentSelector = new(
        new TextDocumentFilter { Language = LanguageId }
    );
    
    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;
    
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, LanguageId);
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var code = request.TextDocument.Text;
        
        _documentManager.UpdateDocument(request.TextDocument.Uri, code);
        
        PublishDiagnostics(request.TextDocument.Uri, code);
        
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var code = request.ContentChanges.First().Text;

        _documentManager.UpdateDocument(request.TextDocument.Uri, code);

        PublishDiagnostics(request.TextDocument.Uri, code);
        
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        _documentManager.RemoveDocument(request.TextDocument.Uri);

        _facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>() 
        });
        
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _documentSelector,
            Change = Change,
            Save = new SaveOptions { IncludeText = false }
        };
    }
    
    private void PublishDiagnostics(DocumentUri uri, string code)
    {
        // 1. Call your core analyzer
        var (parseResult, metaData) = RapidsStaticAnalysis.Analyze(code);
        
        var lspDiagnostics = new List<Diagnostic>();

        // 2. Loop over your custom diagnostics
        foreach (var diagnostic in parseResult.Diagnostics)
        {
            // 3. Convert *your* diagnostic into an *LSP* diagnostic
            
            // A. Get the original index (before pre-processing)
            var sourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index, metaData);

            // B. Get the start line/col
            var (startLine, startCol) = RapidsPreproc.GetRowColFromIndex(sourceIndex, code);
            
            // C. Get the end line/col
            // (Assuming your Token has a Length property)
            int endSourceIndex;
            if (diagnostic.AtEndOfLine)
            {
                // For "missing" tokens, just point at the start
                endSourceIndex = sourceIndex;
            }
            else
            {
                // For existing tokens, get the end of the token
                endSourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index + diagnostic.Token.Value.Length, metaData);
            }
            
            var (endLine, endCol) = RapidsPreproc.GetRowColFromIndex(endSourceIndex, code);

            // D. Create the LSP Diagnostic
            lspDiagnostics.Add(new Diagnostic
            {
                Message = diagnostic.Issue,
                Severity = DiagnosticSeverity.Error, // You can customize this
                Range = new Range(
                    new Position(startLine - 1, startCol - 1), // LSP is 0-indexed
                    new Position(endLine - 1, endCol - 1)
                ),
                Source = "rapids-parser" // Your analyzer's name
            });
        }

        // 4. Send the list to the client
        _facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(lspDiagnostics)
        });
    }
}