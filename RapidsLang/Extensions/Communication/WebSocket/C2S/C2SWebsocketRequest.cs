using System.Text.Json.Serialization;

namespace RapidsLang.Extensions.Communication.WebSocket.C2S;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(C2SSourceData), "source:data")]
[JsonDerivedType(typeof(C2SHello), "extension:hello")]
public class C2SWebsocketRequest
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}