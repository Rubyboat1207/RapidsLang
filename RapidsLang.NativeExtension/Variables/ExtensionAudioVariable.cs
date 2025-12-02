namespace RapidsLang.NativeExtension.Variables;

public class ExtensionAudioVariable(byte[] data) : ExtensionVariable
{
    public byte[] WavData { get; } = data;

    public override string ToString() => $"<Audio Data: {WavData.Length} bytes>";
}