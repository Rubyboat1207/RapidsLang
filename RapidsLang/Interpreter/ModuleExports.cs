using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Types;

namespace RapidsLang.Interpreter;

public class ModuleExports(Dictionary<string, ModuleExport>? exports=null)
{
    // eventuall replace RapidsVariable with something that also holds a type maybe
    public Dictionary<string, ModuleExport> Exports { get; } = exports ?? [];

    public void Add(string name, ModuleExport var)
    {
        Exports[name] = var;
    }
}

public record ModuleExport(RapidsVariable Variable, RapidsType? Type=null);