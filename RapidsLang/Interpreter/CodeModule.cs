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

    public override void Import(InterpreterContext context, List<ImportNode>? importNodes)
    {
        if (!HasRun && !IsRunning)
        {
            IsRunning = true;
            var program = RapidsParser.Parse(Code, out var preprocMetaData);

            var interpreter = new RapidsInterpreter(Code, preprocMetaData, Path);

            interpreter.Context.Exports = Exports;
            interpreter.Context.ModuleRegistry = context.ModuleRegistry;
            
            interpreter.Interpret(program);

            IsRunning = false;
            HasRun = true;
        }
        
        
        base.Import(context, importNodes);
    }
}