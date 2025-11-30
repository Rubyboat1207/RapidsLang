using System.Globalization;

namespace RapidsLang.NativeExtension.Variables;

public class ExtensionNumberVariable(double number) : ExtensionVariable
{
    public double Value { get; } = number;

    public override string ToString() => Value.ToString(CultureInfo.CurrentCulture);
}