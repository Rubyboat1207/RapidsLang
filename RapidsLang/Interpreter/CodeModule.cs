namespace RapidsLang.Interpreter;

public class CodeModule(string code) : Module
{
    private string Code { get; set; } = code;
    
    public override void Import(InterpreterContext context)
    {
        
    }
}