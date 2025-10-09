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
        Console.WriteLine(preprocRes.Output);

        for (var i = 0; i < preprocRes.Output.Length; i++)
        {
            var idx = RapidsPreproc.GetSourceIndexFromProcessedIndex(i, preprocRes.Metadata);
            var row_col = RapidsPreproc.GetRowColFromIndex(idx, code);
            Console.Write($"{row_col.Item1}:{row_col.Item2} => {preprocRes.Output[i]}\n");
        }
        
        var lexResult = RapidsLexer.Lex(preprocRes.Output);


        // TokenType[] expectedTokenTypes =
        // [
        //     TokenType.Pipe,
        //     TokenType.Identifier,
        //     TokenType.Dot,
        //     TokenType.Identifier,
        //     TokenType.StartString,
        //     TokenType.StringContent,
        //     TokenType.OpenCurly,
        //     TokenType.Identifier,
        //     TokenType.ClosedCurly,
        //     TokenType.StringContent,
        //     TokenType.OpenCurly,
        //     TokenType.Identifier,
        //     TokenType.Plus,
        //     TokenType.StartString,
        //     TokenType.StringContent,
        //     TokenType.OpenCurly,
        //     TokenType.StartString,
        //     TokenType.StringContent,
        //     TokenType.EndString,
        //     TokenType.ClosedCurly,
        //     TokenType.EndString,
        //     TokenType.ClosedCurly,
        //     TokenType.EndString,
        //     TokenType.Identifier,
        //     TokenType.Dot,
        //     TokenType.Identifier,
        //     TokenType.SemiColon
        // ];
        //
        //
        // for (var i = 0; i < Math.Max(lexResult.Count, expectedTokenTypes.Length); i++)
        // {
        //     string output = "Got: [";
        //     if (i < lexResult.Count)
        //     {
        //         output += lexResult[i].TokenType + $" Content ({lexResult[i].Value})";
        //     }
        //     else
        //     {
        //         output += "nothing";
        //     }
        //
        //     output += "] Expected: [";
        //     if (i < expectedTokenTypes.Length)
        //     {
        //         output += expectedTokenTypes[i] + "]";
        //         if (i < lexResult.Count && lexResult[i].TokenType == expectedTokenTypes[i])
        //         {
        //             output += " CORRECT (:";
        //         }
        //         else
        //         {
        //             output += " WRONG ):";
        //         }
        //         
        //     }
        //     else
        //     {
        //         output += "nothing] WRONG ):";
        //     }
        //     
        //     Console.WriteLine(output);
        // }
        
        
        var parseResult = RapidsParser.Parse(lexResult);
        
        Console.WriteLine(parseResult);
    }
}