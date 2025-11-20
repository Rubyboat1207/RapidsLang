using System.Text.Json.Serialization;

namespace RapidsLang.Extension.Communication.WebSocket.S2C;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(S2CSourceBeginListening), "source:begin_listening")]
[JsonDerivedType(typeof(S2CSourceEndListening), "source:end_listening")]
[JsonDerivedType(typeof(S2CWriteToTarget), "target:write")]
public class S2CWebsocketRequest
{
    
}