using RapidsLang.Extensions.Manifest;
using RapidsLang.Interpreter;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Extensions;

public class ExtensionModule : Module
{
    private ExtensionData _manifest;
    protected override ModuleExports Exports { get; } = new();

    private bool _hasRun;
    private bool _isRunning;

    public ExtensionModule(ExtensionData manifest)
    {
        _manifest = manifest;
    }
    
    public override void Import(InterpreterContext context, List<ImportNode>? importNodes)
    {
        if (!_hasRun && !_isRunning)
        {
            _isRunning = true;
            var program = RapidsParser.Parse(_manifest.GetMainCodeString(), out var preprocMetaData);

            var interpreter = new RapidsInterpreter(_manifest.GetMainCodeString(), preprocMetaData, _manifest.MainCodePath)
                {
                    Context =
                    {
                        Exports = Exports,
                        ModuleRegistry = context.ModuleRegistry
                    }
                };

            interpreter.Interpret(program);

            _isRunning = false;
            _hasRun = true;
        }
        
        
        base.Import(context, importNodes);
    }
}