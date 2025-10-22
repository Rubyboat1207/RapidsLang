using RapidsLang.Extensions.Communication;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Pipes;

public class DataInputOutput
{
    public DataInputOutput(ExtensionModule module, Identifier sourceIdentifier, bool readable, bool writable)
    {
        Module = module;
        Protocol = module.Extension.ExtensionManifest.Protocol!;
        SourceIdentifier = sourceIdentifier;
        Readable = readable;
        Writable = writable;
        
        Protocol.SubscribeToOutput(sourceIdentifier, new PipeSubscriber(DataListener));
    }

    private ExtensionModule Module { get; }
    private CommunicationProtocol Protocol { get; }
    private Identifier SourceIdentifier { get; }
    public event Action<RapidsVariable>? OnData;

    public bool Readable { get; set; }
    public bool Writable { get; set; }

    public void SendData(RapidsVariable data)
    {
        // this will attempt to send regardless of whether its technically "Writable"
        Protocol.WriteToInput(SourceIdentifier, data);
    }

    private void DataListener(RapidsVariable? variable)
    {
        OnData?.Invoke(variable ?? new RapidsNullVariable());
    }
}