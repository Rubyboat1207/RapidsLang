using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;

namespace RapidsLang.Analyzer;

public static class RapidsStaticAnalysis
{
    public static (RapidsParseResult ParseResult, RapidsPreprocMetaData MetaData) Analyze(string code)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        return (parseResult, preprocRes.Metadata);
    }
}