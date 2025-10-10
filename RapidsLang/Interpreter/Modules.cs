using RapidsLang.Interpreter.Lib.Modules;

namespace RapidsLang.Interpreter;

public static class Modules
{
    public static Dictionary<string, Module> RegisteredModules = new()
    {
        {"console", new ConsoleModule()}
    };
}