namespace RapidsLang.NativeExtension.Variables;

public class ExtensionDataChannel(Identifier identifier, bool readable, bool writable, string? dataVariableName=null) : ExtensionVariable
{
    public Identifier Identifier { get; } = identifier;
    public bool Readable { get; } = readable;
    public bool Writable { get; } = writable;
    public string? DataVariableName { get; } = dataVariableName;
}