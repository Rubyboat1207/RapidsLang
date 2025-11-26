using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using RapidsLang.Analyzer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
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
        AnalyzeAndCache(request.TextDocument.Uri, request.TextDocument.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var code = request.ContentChanges.First().Text;
        AnalyzeAndCache(request.TextDocument.Uri, code);
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
    
    private void AnalyzeAndCache(DocumentUri uri, string code)
    {
        var (parseResult, metaData, staticAnalysisResult) = RapidsStaticAnalysis.Analyze(code, uri.Path);
        
        var parentMap = ParentMapper.BuildParentMap(parseResult.RootNode);

        if (staticAnalysisResult is null)
        {
            _documentManager.UpdateDocument(new AnalyzedDocument(uri, code, parseResult, metaData, staticAnalysisResult, parentMap));
            return;
        }

        var document = new AnalyzedDocument(uri, code, parseResult, metaData, staticAnalysisResult, parentMap);
        
        _documentManager.UpdateDocument(document);
        
        var lspDiagnostics = CalculateDiagnostics(document);

        _facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(lspDiagnostics)
        });
    }

    private List<Diagnostic> CalculateDiagnostics(AnalyzedDocument analyzedDocument)
    {
        var lspDiagnostics = new List<Diagnostic>();

        foreach (var diagnostic in analyzedDocument.ParseResult.Diagnostics)
        {
            var sourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index, analyzedDocument.MetaData);

            var (startLine, startCol) = RapidsPreproc.GetRowColFromIndex(sourceIndex, analyzedDocument.Code);
            
            int endSourceIndex;
            if (diagnostic.AtEndOfLine)
            {
                endSourceIndex = sourceIndex;
            }
            else
            {
                endSourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index + diagnostic.Token.Value.Length, analyzedDocument.MetaData);
            }
            
            var (endLine, endCol) = RapidsPreproc.GetRowColFromIndex(endSourceIndex, analyzedDocument.Code);

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

        if (analyzedDocument.StaticAnalysisResult != null)
        {
            foreach (var diagnostic in analyzedDocument.StaticAnalysisResult.Diagnostics)
            {
                var sourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Index, analyzedDocument.MetaData);
                var endSourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Index + diagnostic.Length, analyzedDocument.MetaData);
                
                var (startLine, startCol) = RapidsPreproc.GetRowColFromIndex(sourceIndex, analyzedDocument.Code);
                var (endLine, endCol) = RapidsPreproc.GetRowColFromIndex(endSourceIndex, analyzedDocument.Code);

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

        return lspDiagnostics;
    }
}