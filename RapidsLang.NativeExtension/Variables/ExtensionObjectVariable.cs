namespace RapidsLang.NativeExtension.Variables;

public class ExtensionObjectVariable(Dictionary<string, ExtensionVariable>? dict=null) : ExtensionVariable
{
    public Dictionary<string, ExtensionVariable> Value { get; } = dict ?? [];
}