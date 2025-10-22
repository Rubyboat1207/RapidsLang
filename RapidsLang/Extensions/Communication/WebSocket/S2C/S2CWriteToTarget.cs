using System.Text.Json;
using System.Text.Json.Serialization;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Communication.WebSocket.S2C;

public class S2CWriteToTarget(RapidsVariable data) : S2CWebsocketRequest
{
    [JsonPropertyName("data")] public RapidsVariable Data { get; set; } = data;
}