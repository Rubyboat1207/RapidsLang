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
        var (parseResult, metaData, staticAnalysisResult) = RapidsStaticAnalysis.Analyze(code);
        
        var lspDiagnostics = new List<Diagnostic>();

        foreach (var diagnostic in parseResult.Diagnostics)
        {
            var sourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index, metaData);

            var (startLine, startCol) = RapidsPreproc.GetRowColFromIndex(sourceIndex, code);
            
            int endSourceIndex;
            if (diagnostic.AtEndOfLine)
            {
                endSourceIndex = sourceIndex;
            }
            else
            {
                endSourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index + diagnostic.Token.Value.Length, metaData);
            }
            
            var (endLine, endCol) = RapidsPreproc.GetRowColFromIndex(endSourceIndex, code);

            lspDiagnostics.Add(new Diagnostic
            {
                Message = diagnostic.Issue,
                Severity = DiagnosticSeverity.Error,
                Range = new Range(
                    new Position(startLine - 1, startCol - 1),
                    new Position(endLine - 1, endCol - 1)
                ),
                Source = "rapids-parser"
            });
        }

        if (staticAnalysisResult != null)
        {
            foreach (var diagnostic in staticAnalysisResult.Diagnostics)
            {
                var sourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Index, metaData);
                var endSourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Index + diagnostic.Length, metaData);
                
                var (startLine, startCol) = RapidsPreproc.GetRowColFromIndex(sourceIndex, code);
                var (endLine, endCol) = RapidsPreproc.GetRowColFromIndex(endSourceIndex, code);

                DiagnosticSeverity severity = diagnostic.Severity switch
                {
                    RapidsStaticAnalysisSeverity.Hint => DiagnosticSeverity.Hint,
                    RapidsStaticAnalysisSeverity.Warning => DiagnosticSeverity.Warning,
                    RapidsStaticAnalysisSeverity.Error => DiagnosticSeverity.Error,
                    _ => DiagnosticSeverity.Warning
                };

                lspDiagnostics.Add(new Diagnostic
                {
                    Message = diagnostic.Message,
                    Severity = severity,
                    Range = new Range(
                        new Position(startLine - 1, startCol - 1),
                        new Position(endLine - 1, endCol - 1)
                    ),
                    Source = "rapids-parser"
                });
            }
        }

        _facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(lspDiagnostics)
        });
    }
}