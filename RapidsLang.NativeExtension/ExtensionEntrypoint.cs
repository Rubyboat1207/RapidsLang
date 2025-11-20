using RapidsLang.NativeExtension.Variables;

namespace RapidsLang.NativeExtension;

public interface IExtensionEntrypoint
{
    public Dictionary<string, ExtensionExport>? Exports { get; }
    public void Init(INativeProtocol protocol);
}