using System.Text.Json.Serialization;

namespace RapidsLang.Extensions.Communication.WebSocket.C2S;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(C2SSourceData), "source:data")]
public class C2SWebsocketRequest
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!;
}