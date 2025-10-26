using RapidsLang.Extensions;
using RapidsLang.Interpreter.Lib.Modules;

namespace RapidsLang.Interpreter;

public class ModuleRegistry
{
    private readonly Dictionary<string, Module> RegisteredModules = new()
    {
        {"console", new ConsoleModule()},
        {"arrays", new ArraysModule()},
        {"strings", new StringsModule()},
        {"time", new TimeModule()},
        {"types", new TypesModule()}
    };

    private readonly HashSet<ExtensionModule> _tickingModules = [];

    public bool TryGetModule(string identifier, out Module? module)
    {
        return RegisteredModules.TryGetValue(identifier, out module);
    }

    public void AddModule(string identifier, Module module)
    {
        RegisteredModules[identifier] = module;
    }

    public void MarkModuleAsTicking(ExtensionModule module, RapidsInterpreter interpreter)
    {
        if (!_tickingModules.Contains(module))
        {
            module.Extension.ExtensionManifest.Protocol?.Init(interpreter);
            _tickingModules.Add(module);
        }
    }

    public void TickExternalModules(InterpreterContext ctx)
    {
        foreach (var extensionModule in _tickingModules)
        {
            extensionModule.Extension.ExtensionManifest.Protocol?.Tick(ctx);
        }
    }
}