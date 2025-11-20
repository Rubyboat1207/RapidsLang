namespace RapidsLang.NativeExtension.Variables;

public class ExtensionFunctionVariable(Action<List<ExtensionVariable>> extensionFunction, int expectedParams) : ExtensionVariable
{
    public Action<List<ExtensionVariable>> ExtensionFunction { get; } = extensionFunction;
    public int ExpectedParams { get; } = expectedParams;
}