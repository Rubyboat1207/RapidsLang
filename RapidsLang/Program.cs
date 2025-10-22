using RapidsLang.Extensions;
using RapidsLang.Interpreter;
using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;

namespace RapidsLang;


public class RapidLangEntry
{
    public static void Main(string[] args)
    {
        string code;

        string? filePath = null;
        // Check if a file path argument is provided
        if (args.Length > 0)
        {
            filePath = args[0];
        
            // Check if the file actually exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found at '{filePath}'");
                return; // Exit if the file doesn't exist
            }
        
            // Read the code from the file
            code = File.ReadAllText(filePath);
        }
        else
        {
            // No file path provided. Check if we are in Debug mode.
#if DEBUG
            // We are in Debug mode and have no args, so use the test program.
            Console.WriteLine("No input file specified. Running debug program...");
            code = TestPrograms.ClosureTests;
#else
        // We are NOT in Debug mode and have no args.
        Console.WriteLine("Error: No input file specified.");
        Console.WriteLine("Usage: rapids <source_file>.rpd");
        return; // Exit the program
#endif
        }

        // --- The rest of your program logic is unchanged ---

        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);
    
        var interpreter = new RapidsInterpreter(code, preprocRes.Metadata, filePath);
        interpreter.Interpret(parseResult);
    }
}