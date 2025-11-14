using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public class CodeModule(string code, string? path=null) : Module
{
    private string Code { get; } = code;
    private string? Path { get; } = path;

    protected override ModuleExports Exports { get; } = new();
    private bool HasRun;
    private bool IsRunning;

    public override void Import(RapidsInterpreter interpreter, List<ImportNode>? importNodes)
    {
        var context = interpreter.Context;
        if (!HasRun && !IsRunning)
        {
            IsRunning = true;
            var program = RapidsParser.Parse(Code, out var preprocMetaData);

            if (program.Diagnostics.Count > 0)
            {
                program.PrintDiagnostics(Path ?? "internal", Code, preprocMetaData);
                throw new Exception($"Failed to parse module at {Path}. See above diagnostics");
            }

            var moduleInterpreter = new RapidsInterpreter(Code, preprocMetaData, Path)
            {
                Context =
                {
                    Exports = Exports,
                    ModuleRegistry = context.ModuleRegistry
                }
            };


            moduleInterpreter.Interpret(program.RootNode).Wait();

            IsRunning = false;
            HasRun = true;
        }
        
        
        base.Import(interpreter, importNodes);
    }
}