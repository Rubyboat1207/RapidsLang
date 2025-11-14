using System.Text.Json.Serialization;
using JetBrains.Annotations;
using RapidsLang.Extensions.Channel;

namespace RapidsLang.Extensions.Communication.WebSocket.S2C;

public class S2CSourceEndListening(Identifier identifier) : S2CWebsocketRequest
{
    [JsonPropertyName("identifier")]
    public Identifier Identifier { get; set; } = identifier;
}