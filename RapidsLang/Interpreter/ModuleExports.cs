using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter;

public class ModuleExports(Dictionary<string, RapidsVariable>? exports=null)
{
    // eventuall replace RapidsVariable with something that also holds a type maybe
    public Dictionary<string, RapidsVariable> Exports { get; } = exports ?? [];

    public void Add(string name, RapidsVariable var)
    {
        Exports[name] = var;
    }
}