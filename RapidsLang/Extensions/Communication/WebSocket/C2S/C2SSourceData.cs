using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RapidsLang.Extensions.Channel;

namespace RapidsLang.Extensions.Communication.WebSocket.C2S;

public class C2SSourceData : C2SWebsocketRequest
{
    [JsonPropertyName("identifier")]
    public required Identifier Identifier { get; set; }
    
    [JsonPropertyName("data")]
    public required JsonElement Data { get; set; }
}