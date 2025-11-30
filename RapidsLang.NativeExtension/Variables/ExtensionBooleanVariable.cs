namespace RapidsLang.NativeExtension.Variables;

public class ExtensionBooleanVariable(bool value) : ExtensionVariable
{
    public bool Value { get; } = value;

    public override string ToString() => Value ? "true" : "false";
}