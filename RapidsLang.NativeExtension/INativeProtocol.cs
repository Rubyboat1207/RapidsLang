using RapidsLang.NativeExtension.Variables;

namespace RapidsLang.NativeExtension;

public interface INativeProtocol
{
    public event Action<Identifier> OutputEnabled;
    public event Action<Identifier> OutputDisabled;

    public void WriteToOutput(Identifier identifier, ExtensionVariable variable);
    public void RegisterInput(Identifier identifier, Action<ExtensionVariable?> variable);
}