using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RapidsLang.Analyzer;

namespace RapidsLang.LanguageServer;

public class RapidsSemanticTokensHandler(DocumentManager documentManager) : SemanticTokensHandlerBase
{
    private readonly DocumentManager _documentManager = documentManager;
    
    public static readonly SemanticTokensLegend Legend = new()
    {
        TokenTypes = new Container<SemanticTokenType>(
            SemanticTokenType.Keyword,    
            SemanticTokenType.Function,   
            SemanticTokenType.Parameter,  
            SemanticTokenType.Variable,   
            SemanticTokenType.String,     
            SemanticTokenType.Number,     
            SemanticTokenType.Type,       
            SemanticTokenType.Namespace,
            SemanticTokenType.Operator,
            SemanticTokenType.Comment
        ),
        TokenModifiers = new Container<SemanticTokenModifier>(
            SemanticTokenModifier.Readonly
        )
    };

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SemanticTokensRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("rapidslang"),
            Legend = Legend,
            Full = new SemanticTokensCapabilityRequestFull
            {
                Delta = true
            },
            Range = true
        };
    }

    protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
    {
        var analysis = _documentManager.GetDocument(identifier.TextDocument.Uri);

        if (analysis is null)
        {
            return Task.CompletedTask;
        }

        var visitor = new SemanticTokenVisitor(builder, analysis.MetaData, analysis.Code, analysis.StaticAnalysisResult, analysis.ParentMap);
        
        visitor.Visit(analysis.ParseResult.RootNode);
        
        visitor.FlushRemainingComments();

        return Task.CompletedTask;
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SemanticTokensDocument(Legend));
    }
}