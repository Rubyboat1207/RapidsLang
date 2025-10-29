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

public class RapidsStaticAnalysisScope
{
    public RapidsStaticAnalysisResult? Parent;
    public List<Symbol> Symbols = [];
}

public class RapidsStaticAnalysisResult
{
    public List<RapidsStaticAnalysisDiagnostic> Diagnostics { get; } = [];
    public Dictionary<StatementsNode, RapidsStaticAnalysisScope> Scope = [];
}

public static class RapidsStaticAnalysis
{
    public static (RapidsParseResult ParseResult, RapidsPreprocMetaData MetaData, RapidsStaticAnalysisResult? analysis) Analyze(string code)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        RapidsStaticAnalysisResult? rapidsStaticAnalysisResult = null;

        if (parseResult.Diagnostics.Count > 0)
        {
            rapidsStaticAnalysisResult = StaticAnalysis(preprocRes.Metadata, parseResult.RootNode);
        }

        return (parseResult, preprocRes.Metadata, rapidsStaticAnalysisResult);
    }

    public static RapidsStaticAnalysisResult StaticAnalysis(RapidsPreprocMetaData metaData, StatementsNode rootNode)
    {
        return null;
    }
}