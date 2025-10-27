using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;

namespace RapidsLang.LanguageServer;

public static class RapidsAnalyzer
{
    public static (RapidsParseResult ParseResult, RapidsPreprocMetaData MetaData) Analyze(string code)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        return (parseResult, preprocRes.Metadata);
    }
}