using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RapidsLang.Analyzer;
using RapidsLang.Extension;
using RapidsLang.Extensions;
using RapidsLang.Interpreter;
using RapidsLang.InterpreterVM;
using RapidsLang.Lexer;
using RapidsLang.Parser;
using RapidsLang.PreProcessor;
using RapidsLang.Utils;

namespace RapidsLang;


public class RapidLangEntry
{
    public static int Main(string[] args)
    {
        string? code = null;
        RapidProgram? program = null;

        string? filePath = null;
        // Check if a file path argument is provided
        if (args.Length > 0)
        {
            filePath = args[0];
        
            // Check if the file actually exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found at '{filePath}'");
                return 1; // Exit if the file doesn't exist
            }
        
            // Read the code from the file
            var binary = File.ReadAllBytes(filePath);

            if (BytecodeHeader.Signature.SequenceEqual(binary.AsSpan(0, 6).ToArray()))
            {
                program = RapidProgram.FromBytes(binary);
            }
            else
            {
                code = Encoding.UTF8.GetString(binary);
            }
            
            
        }
        else
        {
            Console.WriteLine("Error: No input file specified.");
            Console.WriteLine("Usage: rapids <source_file>.rpd");
            return 1; // Exit the program
        }

        var useVm = args.Contains("--vm");
        var outputFile = args.Contains("-o");
        var produceDisassembly = args.Contains("--ds");

        if (program is not null)
        {
            if (produceDisassembly)
            {
                File.WriteAllText("debug.rpdbd", program.Disassemble());
            }
            var vm = new RapidsVirtualMachine();
        
            vm.Run(program);

            return 0;
        }

        if (code is null)
        {
            Console.WriteLine("Code file not found");
            return 1;
        }
        
        if(args.Contains("--lint"))
        {
            var (parseRes, metaData, analysis) = RapidsStaticAnalysis.Analyze(code, filePath);
            
            parseRes.PrintDiagnostics(filePath, code, metaData);
            analysis?.PrintDiagnostics(filePath, code, metaData);
            
            return 0;
        }
        
        if (useVm || outputFile)
        {
            var (parseRes, metaData, analysis) = RapidsStaticAnalysis.Analyze(code, filePath);
            
            if (analysis is null)
            {
                Console.WriteLine("Failed to compile.");
                parseRes.PrintDiagnostics("internal", code, metaData);
                return 1;
            }
            
            program = RapidsCompiler.Compile(parseRes.RootNode, analysis);

            if (outputFile)
            {
                var path = args[Array.IndexOf(args, "-o") + 1];
                if (!Path.HasExtension(path))
                {
                    path += ".rpdb";
                }
                File.WriteAllBytes(path, program.ToBytes());
            }
            
            if (produceDisassembly)
            {
                File.WriteAllText("debug.rpdbd", program.Disassemble());

                return 0;
            }

            if (!useVm)
            {
                return 0;
            }

            var vm = new RapidsVirtualMachine();
        
            vm.Run(program);

            return 0;
        }
        
        var preprocRes = RapidsPreproc.Preprocess(code);
        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        var parseResult = RapidsParser.Parse(lexResult);

        if (parseResult.Diagnostics.Count != 0)
        {
            parseResult.PrintDiagnostics(filePath ?? "debug", code, preprocRes.Metadata);
            return 0;
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

        return 0;
    }

    public static void MainVMBytecode()
    {
        var vm = new RapidsVirtualMachine();
        
        vm.Run(new RapidProgram
        {
            Header = new BytecodeHeader
            {
                GlobalsCount = 1,
                Modules = [new ModuleImport("console", ["print"])],
                Strings = [
                    "Hello, World"
                ]
            },
            Code = [
                new LoadString(0),
                new LoadGlobal(0),
                new Call()
            ]
        });
    }

    public static void MainVMCode()
    {
        var code = "use console: print; print(`Hello, World`);";
        
        var (parseRes, metaData, analysis) = RapidsStaticAnalysis.Analyze(code, "internal");

        if (analysis is null)
        {
            Console.WriteLine("Failed to compile.");
            parseRes.PrintDiagnostics("internal", code, metaData);
            return;
        }

        var program = RapidsCompiler.Compile(parseRes.RootNode, analysis);

        var vm = new RapidsVirtualMachine();
        
        vm.Run(program);
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