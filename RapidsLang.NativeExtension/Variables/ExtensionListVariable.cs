namespace RapidsLang.NativeExtension.Variables;

public class ExtensionListVariable(List<ExtensionVariable>? list=null) : ExtensionVariable
{
    public List<ExtensionVariable> Value { get; } = list ?? [];
}