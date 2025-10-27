using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace RapidsLang.LanguageServer;

public class DocumentManager
{
    private readonly ConcurrentDictionary<DocumentUri, string> _documents = new();

    public void UpdateDocument(DocumentUri uri, string text)
    {
        _documents[uri] = text;
    }

    public string? GetDocument(DocumentUri uri)
    {
        _documents.TryGetValue(uri, out var text);
        return text;
    }

    public void RemoveDocument(DocumentUri uri)
    {
        _documents.TryRemove(uri, out _);
    }
}