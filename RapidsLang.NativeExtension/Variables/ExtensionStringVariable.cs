namespace RapidsLang.NativeExtension.Variables;

public class ExtensionStringVariable(string str) : ExtensionVariable
{
    public string Value { get; } = str;
}