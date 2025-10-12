using RapidsLang.Interpreter.Lib.Modules;

namespace RapidsLang.Interpreter;

public static class Modules
{
    public static readonly Dictionary<string, Module> RegisteredModules = new()
    {
        {"console", new ConsoleModule()},
        {"arrays", new ArraysModule()},
        {"strings", new StringsModule()}
    };
}