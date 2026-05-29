using System.Diagnostics.CodeAnalysis;
using RapidsLang.Extensions;
using RapidsLang.Interpreter.Lib.Modules;

namespace RapidsLang.Interpreter;

public class ModuleRegistry
{
    private readonly Dictionary<string, Module> RegisteredModules = new(NativeModules);

    public static readonly Dictionary<string, Module> NativeModules = new()
    {
        { "console", new ConsoleModule() },
        { "arrays", new ArraysModule() },
        { "strings", new StringsModule() },
        { "time", new TimeModule() },
        { "types", new TypesModule() },
        { "random", new RandomModule() },
        { "math", new MathModule() },
        { "program", new ProgramModule() },
        { "env", new EnvironmentModule() },
        { "files", new FilesModule() },
        { "queue", new QueueModule() }
    };

    private readonly HashSet<Module> _tickingModules = [];
    
    public bool TryGetModule(string identifier, [NotNullWhen(true)] out Module? module)
    {
        return RegisteredModules.TryGetValue(identifier, out module);
    }

    public Module GetModule(string identifier)
    {
        return RegisteredModules[identifier];
    }

    public void AddModule(string identifier, Module module)
    {
        RegisteredModules[identifier] = module;
    }

    public void MarkModuleAsTicking(Module module, RapidsInterpreter interpreter)
    {
        if (!_tickingModules.Contains(module))
        {
            module.Protocol?.Init(interpreter, null);
            _tickingModules.Add(module);
        }
    }

    public void TickExternalModules(InterpreterContext ctx)
    {
        foreach (var extensionModule in _tickingModules)
        {
            extensionModule.Protocol?.Tick(ctx);
        }
    }
}