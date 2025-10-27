using RapidsLang.Extensions.Manifest;
using RapidsLang.Interpreter;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Extensions;

public class ExtensionModule : Module
{
    public ExtensionData Extension { get; }
    protected override ModuleExports Exports { get; } = new();

    private bool _hasRun;
    private bool _isRunning;

    public ExtensionModule(ExtensionData extension)
    {
        Extension = extension;
    }
    
    public override void Import(InterpreterContext context, List<ImportNode>? importNodes)
    {
        if (!_hasRun && !_isRunning)
        {
            _isRunning = true;
            var program = RapidsParser.Parse(Extension.GetMainCodeString(), out var preprocMetaData);

            var interpreter = new RapidsInterpreter(Extension.GetMainCodeString(), preprocMetaData, Extension.MainCodePath)
                {
                    Context =
                    {
                        Exports = Exports,
                        ModuleRegistry = context.ModuleRegistry,
                        CurrentModule = this
                    }
                };

            if (program.Diagnostics.Count > 0)
            {
                program.PrintDiagnostics(Extension.MainCodePath, Extension.GetMainCodeString(), preprocMetaData);

                throw new Exception("Failed to parse because of above reasons");
            }

            interpreter.Interpret(program.RootNode).Wait();

            _isRunning = false;
            _hasRun = true;
        }
        
        
        base.Import(context, importNodes);
    }
}