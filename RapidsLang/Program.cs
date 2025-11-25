using System.Runtime.InteropServices;
using RapidsLang.Analyzer;
using RapidsLang.Extensions;
using RapidsLang.Interpreter;
using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;
using RapidsLang.Utils;

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
            Console.WriteLine("Error: No input file specified.");
            Console.WriteLine("Usage: rapids <source_file>.rpd");
            return; // Exit the program
        }
        
        if(args.Contains("--lint"))
        {
            var (parseRes, metaData, analysis) = RapidsStaticAnalysis.Analyze(code);
            
            parseRes.PrintDiagnostics(filePath, code, metaData);
            analysis?.PrintDiagnostics(filePath, code, metaData);
            
            return;
        }
        
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        if (parseResult.Diagnostics.Count != 0)
        {
            parseResult.PrintDiagnostics(filePath ?? "debug", code, preprocRes.Metadata);
            return;
        }

        var extensions = ExtensionLoader.GetExternalExtensions();

        var interpreter = new RapidsInterpreter(code, preprocRes.Metadata, filePath, supportsOnStatements:true);
        
        extensions.ForEach(d => interpreter.Context.ModuleRegistry.AddModule(d.ExtensionManifest.ModuleName, new ExtensionModule(d)));

        SetupExitHandlers(interpreter);
        
        Env.Load();
        
        try
        {
            interpreter.Interpret(parseResult.RootNode, true).Wait();
        }
        finally
        {
            interpreter.HandleExit().Wait();
        }

        
    }
    
    private static void SetupExitHandlers(RapidsInterpreter interpreter)
    {
        if (OperatingSystem.IsWindows())
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                
                interpreter.HandleExit().Wait();
                
                Environment.Exit(0);
            };
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            Action<PosixSignalContext> handler = ctx =>
            {
                ctx.Cancel = true;

                interpreter.HandleExit().Wait();

                Environment.Exit(0);
            };

            PosixSignalRegistration.Create(PosixSignal.SIGINT, handler);
            
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, handler);
        }
    }
}