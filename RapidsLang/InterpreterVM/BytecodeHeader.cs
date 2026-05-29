using RapidsLang.Parser.Nodes;

namespace RapidsLang.InterpreterVM;

public struct BytecodeHeader
{
    public int Version;
    public ModuleImport[] Modules;
    public string[] Strings;
    public int GlobalsCount;
    public int OutermostLocalsCount;
}