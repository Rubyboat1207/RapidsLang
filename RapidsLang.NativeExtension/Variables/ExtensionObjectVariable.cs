namespace RapidsLang.NativeExtension.Variables;

public class ExtensionObjectVariable(Dictionary<string, ExtensionVariable>? dict=null) : ExtensionVariable
{
    public Dictionary<string, ExtensionVariable> Value { get; } = dict ?? [];

    public override string ToString() => "{" + string.Join(",", Value.Select(s => s.Value + ": " + s.Key.ToString())) + "}";
}