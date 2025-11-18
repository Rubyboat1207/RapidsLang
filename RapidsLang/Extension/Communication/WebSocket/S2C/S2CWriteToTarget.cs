using System.Text.Json;
using System.Text.Json.Serialization;
using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Communication.WebSocket.S2C;

public class S2CWriteToTarget(Identifier targetIdentifier, RapidsVariable data) : S2CWebsocketRequest
{
    [JsonPropertyName("target_identifier")]
    public Identifier TargetIdentifier { get; set; } = targetIdentifier;
    
    [JsonPropertyName("data")] 
    public RapidsVariable Data { get; set; } = data;
}