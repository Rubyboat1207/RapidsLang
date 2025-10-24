using RapidsLang.Extensions.Communication;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Pipes;

public class DataInputOutput
{
    public DataInputOutput(ExtensionModule module, Identifier sourceIdentifier, bool readable, bool writable)
    {
        _subscriber = new PipeSubscriber(DataListener);
        Module = module;
        Protocol = module.Extension.ExtensionManifest.Protocol!;
        SourceIdentifier = sourceIdentifier;
        Readable = readable;
        Writable = writable;
    }

    private ExtensionModule Module { get; }
    private CommunicationProtocol Protocol { get; }
    private Identifier SourceIdentifier { get; }
    private event Action<RapidsVariable>? _onData;
    private int _dataSubscriberCount = 0;
    private PipeSubscriber _subscriber;
    public string? DataVariableName { get; set; }
    public event Action<RapidsVariable>? OnData
    {
        add
        {
            if (_dataSubscriberCount == 0)
            {
                Protocol.SubscribeToOutput(SourceIdentifier, _subscriber);
            }

            _dataSubscriberCount++;
            _onData += value;
            
        }
        remove
        {
            _dataSubscriberCount--;
            if (_dataSubscriberCount == 0)
            {
                Protocol.UnsubscribeToOutput(SourceIdentifier, _subscriber.Guid);
            }

            _onData -= value;
        }
    }

    public bool Readable { get; set; }
    public bool Writable { get; set; }

    public void SendData(RapidsVariable data)
    {
        // this will attempt to send regardless of whether its technically "Writable"
        Protocol.WriteToInput(SourceIdentifier, data);
    }

    private void DataListener(RapidsVariable? variable)
    {
        _onData?.Invoke(variable ?? new RapidsNullVariable());
    }
}