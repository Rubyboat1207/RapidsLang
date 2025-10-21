using RapidsLang.Interpreter.Lib.Modules;

namespace RapidsLang.Interpreter;

public class ModuleRegistry
{
    private readonly Dictionary<string, Module> RegisteredModules = new()
    {
        {"console", new ConsoleModule()},
        {"arrays", new ArraysModule()},
        {"strings", new StringsModule()}
    };

    public bool TryGetModule(string identifier, out Module? module)
    {
        return RegisteredModules.TryGetValue(identifier, out module);
    }

    public void AddModule(string identifier, Module module)
    {
        
    }
}