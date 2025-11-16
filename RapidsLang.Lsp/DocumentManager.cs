using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol;
using RapidsLang.Analyzer;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;

namespace RapidsLang.LanguageServer;

public class AnalyzedDocument(
    DocumentUri uri,
    string code,
    RapidsParseResult parseResult,
    RapidsPreprocMetaData metaData,
    RapidsStaticAnalysisResult? staticAnalysisResult,
    Dictionary<Node, Node> parentMap
)
{
    public DocumentUri Uri { get; } = uri;
    public string Code { get; } = code;
    public RapidsParseResult ParseResult { get; } = parseResult;
    public RapidsPreprocMetaData MetaData { get; } = metaData;
    public RapidsStaticAnalysisResult? StaticAnalysisResult { get; } = staticAnalysisResult;
    public Dictionary<Node, Node> ParentMap { get; } = parentMap;
}

public class DocumentManager
{
    private readonly ConcurrentDictionary<DocumentUri, AnalyzedDocument> _documents = new();

    public void UpdateDocument(AnalyzedDocument doc)
    {
        _documents[doc.Uri] = doc;
    }

    public AnalyzedDocument? GetDocument(DocumentUri uri)
    {
        _documents.TryGetValue(uri, out var doc);
        return doc;
    }
    
    

    public void RemoveDocument(DocumentUri uri)
    {
        _documents.TryRemove(uri, out _);
    }
}