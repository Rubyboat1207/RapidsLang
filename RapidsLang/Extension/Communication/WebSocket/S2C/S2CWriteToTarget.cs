using System.Text.Json.Serialization;
using RapidsLang.Extension.Channel;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extension.Communication.WebSocket.S2C;

public class S2CWriteToTarget(Identifier targetIdentifier, RapidsVariable data) : S2CWebsocketRequest
{
    [JsonPropertyName("target_identifier")]
    public Identifier TargetIdentifier { get; set; } = targetIdentifier;
    
    [JsonPropertyName("data")] 
    public RapidsVariable Data { get; set; } = data;
}