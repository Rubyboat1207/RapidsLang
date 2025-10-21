using System.Text.Json.Serialization;
using RapidsLang.Extensions.Communication.WebSocket;
using RapidsLang.Extensions.Pipes;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Communication;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WebsocketProtocol), "ws")]
public abstract class CommunicationProtocol
{
    public required string Type { get; set; }

    public abstract PipeWriteResult WriteToInput(Identifier identifier, RapidsVariable? value);
    public abstract void SubscribeToOutput(Identifier identifier, PipeSubscriber subscriber);
    public abstract void UnsubscribeToOutput(Identifier identifier, Guid guid);

    public virtual void Init() { }
    public abstract void Tick(InterpreterContext ctx);
}

public abstract class PipeWriteResult;

public class GoodPipeWriteResult : PipeWriteResult;

public class BadPipeWriteResult(string errorType, string error) : PipeWriteResult
{
    public string ErrorType { get; } = errorType;
    public string Error { get; } = error;
}

public class PipeSubscriber(Action<RapidsVariable?> @event)
{
    public Action<RapidsVariable?> Event { get; set; } = @event;
    public Guid Guid = Guid.CreateVersion7();
}