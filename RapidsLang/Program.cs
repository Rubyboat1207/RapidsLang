using RapidsLang.Interpreter;
using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;

namespace RapidsLang;


public class RapidLangEntry
{
    public static void Main(string[] args)
    {
        var code = TestPrograms.ListTest;
        var preprocRes = RapidsPreproc.Preprocess(code);

        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        
        var parseResult = RapidsParser.Parse(lexResult);

        var interpreter = new RapidsInterpreter(code, preprocRes.Metadata, parseResult);

        interpreter.Interpret();
    }
}