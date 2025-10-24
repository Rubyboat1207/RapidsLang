using System.Text.Json;
using System.Text.Json.Serialization;

namespace RapidsLang.Interpreter.Variables;

public class RapidsVariableJsonConverter : JsonConverter<RapidsVariable>
{
    public override RapidsVariable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // I will not be implementing this.
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, RapidsVariable value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case RapidsBooleanVariable booleanVariable:
                writer.WriteBooleanValue(booleanVariable.Value);
                break;
            case RapidsListVariable listVariable:
            {
                writer.WriteStartArray();

                foreach (var rapidsVariable in listVariable.List)
                {
                    Write(writer, rapidsVariable, options);
                }
            
                writer.WriteEndArray();
                break;
            }
            case RapidsNullVariable:
            {
                writer.WriteNullValue();
                break;
            }
            case RapidsNumberVariable numberVariable:
            {
                writer.WriteNumberValue(numberVariable.Value);
                break;
            }
            case RapidsObjectVariable objectVariable:
            {
                writer.WriteStartObject();
                
                foreach (var rapidsVariable in objectVariable.ObjectValues)
                {
                    writer.WritePropertyName(rapidsVariable.Key);
                    Write(writer, rapidsVariable.Value, options);
                }

                break;
            }
            case RapidsStringVariable str:
            {
                writer.WriteStringValue(str.Value);
                break;
            }
            case RapidsFunctionReferenceVariable:
            case RapidsDataChannelVariable:
                break; // emit nothing
        }
    }
}