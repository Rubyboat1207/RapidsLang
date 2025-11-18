using System.Text.Json.Serialization;
using RapidsLang.Extensions.Communication.WebSocket;
using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Communication;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WebsocketProtocol), "ws")]
public abstract class CommunicationProtocol
{
    protected RapidsInterpreter? ResponsibleInterpreter { get; set; }
    public abstract ChannelWriteResult WriteToInput(Identifier identifier, RapidsVariable? value);

    public virtual void SubscribeToOutput(Identifier identifier, ChannelSubscriber subscriber)
    {
        if (!EventListeners.TryGetValue(identifier, out Dictionary<Guid, ChannelSubscriber>? value))
        {
            value = [];
            EventListeners[identifier] = value;
            OutputAdded(identifier);
        }

        value[subscriber.Guid] = subscriber;
    }

    public virtual void UnsubscribeToOutput(Identifier identifier, Guid guid)
    {
        EventListeners[identifier].Remove(guid);

        if (EventListeners[identifier].Count == 0)
        {
            EventListeners.Remove(identifier);
            OutputRemoved(identifier);
        }
    }

    protected virtual void OutputRemoved(Identifier identifier) { }

    protected virtual void OutputAdded(Identifier identifier) { }

    public virtual void Init(RapidsInterpreter interpreter)
    {
        ResponsibleInterpreter = interpreter;
    }
    public abstract void Tick(InterpreterContext ctx);
    
    protected Dictionary<Identifier, Dictionary<Guid, ChannelSubscriber>> EventListeners { get; } = [];
}

public abstract class ChannelWriteResult;

public class GoodChannelWriteResult : ChannelWriteResult;

public class BadChannelWriteResult(string errorType, string error) : ChannelWriteResult
{
    public string ErrorType { get; } = errorType;
    public string Error { get; } = error;
}

public class ChannelSubscriber(Action<RapidsVariable?> @event)
{
    public Action<RapidsVariable?> Event { get; set; } = @event;
    public Guid Guid = Guid.CreateVersion7();
}