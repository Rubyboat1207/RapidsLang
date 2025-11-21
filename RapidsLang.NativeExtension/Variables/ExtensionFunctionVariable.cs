namespace RapidsLang.NativeExtension.Variables;

public class ExtensionFunctionVariable(Func<List<ExtensionVariable>, ExtensionVariable?>  extensionFunction, int expectedParams) : ExtensionVariable
{
    public Func<List<ExtensionVariable>, ExtensionVariable?> ExtensionFunction { get; } = extensionFunction;
    public int ExpectedParams { get; } = expectedParams;
}