namespace RapidsLang.InterpreterVM;

public readonly struct ModuleImport(string name, string[] imports)
{
    public string ModuleName { get; } = name;
    public string[] Imports { get; } = imports;
}