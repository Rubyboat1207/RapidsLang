namespace RapidsLang.NativeExtension.Variables;

public class ExtensionNumberVariable(double number) : ExtensionVariable
{
    public double Value { get; } = number;
}