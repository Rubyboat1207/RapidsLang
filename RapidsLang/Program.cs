using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;

namespace RapidsLang;


public class RapidLangEntry
{
    public static void Main(string[] args)
    {
        var code = TestPrograms.HelloWorld;
        var preprocRes = RapidsPreproc.Preprocess(code);

        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        
        var parseResult = RapidsParser.Parse(lexResult);
        
        Console.WriteLine(parseResult);
    }
}