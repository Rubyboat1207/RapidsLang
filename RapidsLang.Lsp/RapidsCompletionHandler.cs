using RapidsLang.Analyzer;
using RapidsLang.Parser.Nodes;
using RapidsLang.Parser.Types;
using RapidsLang.PreProcessor;

namespace RapidsLang.LanguageServer;

using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading;
using System.Threading.Tasks;

public class RapidsCompletionHandler : CompletionHandlerBase
{
    // You'll need to inject your document manager
    private readonly DocumentManager _documentManager;

    public RapidsCompletionHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public override async Task<CompletionList> Handle(
        CompletionParams request, 
        CancellationToken cancellationToken)
    {
        var items = new List<CompletionItem>();

        var analyzedDoc = _documentManager.GetDocument(request.TextDocument.Uri);
        if (analyzedDoc?.StaticAnalysisResult == null)
        {
            return new CompletionList();
        }

        var code = analyzedDoc.Code;
        var processedIndex =
            RapidsPreproc.GetIndexFromRowCol(request.Position.Line + 1, request.Position.Character + 1, code);

        if (processedIndex > 0 && code[processedIndex - 1] == '.')
        {
            var leftNode = analyzedDoc.ParseResult.FindNodeAt(processedIndex - 2);
            if (leftNode is ExpressionNode leftExpression)
            {
                if (analyzedDoc.StaticAnalysisResult.ExpressionTypes.TryGetValue(leftExpression, out var leftType))
                {
                    var members = leftType.GetMembers();
                    
                    foreach (var (name, type) in members)
                    {
                        items.Add(CreateCompletionItem(name, type));
                    }
                }
            }
        }
        else
        {
            // --- Handle Scope Trigger (non-dot) ---
            // The user is just typing. Suggest variables and keywords.
            var scope = GetScopeAt(analyzedDoc, processedIndex);
            if (scope != null)
            {
                foreach (var symbol in scope.Symbols)
                {
                    items.Add(CreateCompletionItem(symbol.Name, symbol.Type, symbol));
                }
            }
            
            // todo: Add keywords, snippets, etc.
        }

        return new CompletionList(items);
    }

    // Helper to create a CompletionItem
    private CompletionItem CreateCompletionItem(string name, RapidsType type, Symbol? symbol = null)
    {
        var kind = CompletionItemKind.Property; // Default
        
        if (type is RapidsFunctionType)
        {
            kind = CompletionItemKind.Method;
        }
        else if (symbol != null)
        {
            if (symbol.IsArgument) kind = CompletionItemKind.Variable; // Or Parameter
            else if (symbol.Type is RapidsFunctionType) kind = CompletionItemKind.Function;
            else kind = CompletionItemKind.Variable;
        }

        return new CompletionItem
        {
            Label = name,
            Kind = kind,
            Detail = type.Name, // Shows the type name in the autocomplete menu
            // Documentation = "..." // You can add descriptions here
        };
    }

    // Helper to find the scope at the current cursor
    private RapidsStaticAnalysisScope? GetScopeAt(AnalyzedDocument doc, int index)
    {
        var node = doc.ParseResult.FindNodeAt(index);
        if (node == null) return null;

        var owningBlock = node.GetAncestor<StatementsNode>(doc.ParentMap);
        if (owningBlock != null && 
            doc.StaticAnalysisResult.Scopes.TryGetValue(owningBlock, out var scope))
        {
            return scope;
        }
        return null;
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability, 
        ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("rapidslang"),
            TriggerCharacters = new Container<string>("."),
            AllCommitCharacters = new Container<string>("."),
            ResolveProvider = false
        };
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request);
    }
}