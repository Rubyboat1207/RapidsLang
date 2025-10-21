using RapidsLang.Extensions.Communication;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Pipes;

public class DataInputOutput(CommunicationProtocol protocol, Identifier sourceIdentifier, bool readable, bool writable)
{
    private CommunicationProtocol Protocol { get; } = protocol;
    private Identifier SourceIdentifier { get; } = sourceIdentifier;

    public bool Readable { get; set; } = readable;
    public bool Writable { get; set; } = writable;

    public void SendData(RapidsVariable data)
    {
        // this will attempt to send regardless of whether its technically "Writable"
        Protocol.WriteToInput(SourceIdentifier, data);
    }
}