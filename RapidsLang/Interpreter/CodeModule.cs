using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public class CodeModule(string code) : Module
{
    private string Code { get; set; } = code;

    protected override ModuleExports Exports { get; } = new();
    private bool HasRun;
    private bool IsRunning;

    public override void Import(InterpreterContext context, List<ImportNode>? importNodes)
    {
        if (!HasRun && !IsRunning)
        {
            IsRunning = true;
            var program = RapidsParser.Parse(Code, out var preprocMetaData);

            var interpreter = new RapidsInterpreter(Code, preprocMetaData, program);

            interpreter.Context.Exports = Exports;
            
            interpreter.Interpret();

            IsRunning = false;
            HasRun = true;
        }
        
        
        base.Import(context, importNodes);
    }
}