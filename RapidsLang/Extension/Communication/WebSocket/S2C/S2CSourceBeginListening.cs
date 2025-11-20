using System.Text.Json.Serialization;
using RapidsLang.Extension.Channel;

namespace RapidsLang.Extension.Communication.WebSocket.S2C;

public class S2CSourceBeginListening(Identifier identifier) : S2CWebsocketRequest
{
    [JsonPropertyName("identifier")]
    public Identifier Identifier { get; set; } = identifier;
}