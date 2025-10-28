using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;

namespace RapidsLang.Analyzer;

public enum RapidsStaticAnalysisSeverity
{
    Hint,
    Warning,
    Error
}

public class RapidsStaticAnalysisDiagnostic(string message, int sourceIndex, int length, RapidsStaticAnalysisSeverity severity)
{
    public string Message { get; } = message;
    public int SourceIndex { get; } = sourceIndex;
    public int Length { get; } = length;
    public RapidsStaticAnalysisSeverity Severity { get; } = severity;
}

public class Symbol(string name, bool isConstant)
{
    public string Name { get; } = name;
    public bool IsConstant { get; } = isConstant;
    public bool IsMutated { get; set; } = false;
}

public class RapidsSemanticModel
{
    public Dictionary<Node, Symbol> ResolvedSymbols { get; } = new();
}

public static class RapidsStaticAnalysis
{
    public static (RapidsParseResult ParseResult, RapidsPreprocMetaData MetaData) Analyze(string code)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        return (parseResult, preprocRes.Metadata);
    }

    // public static RapidsSemanticModel StaticAnalysis(RapidsPreprocMetaData MetaData, StatementsNode RootNode)
    // {
    //     
    // }
}