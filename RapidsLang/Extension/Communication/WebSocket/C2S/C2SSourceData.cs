using System.Text.Json;
using System.Text.Json.Serialization;
using RapidsLang.Extension.Channel;

namespace RapidsLang.Extension.Communication.WebSocket.C2S;

public class C2SSourceData : C2SWebsocketRequest
{
    [JsonPropertyName("identifier")]
    public required Identifier Identifier { get; set; }
    
    [JsonPropertyName("data")]
    public required JsonElement Data { get; set; }
}