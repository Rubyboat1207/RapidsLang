using System.Text.Json;

namespace RapidsLang.Interpreter.Variables;

public abstract class RapidsVariable
{
    public abstract RapidsVariable? GetResult(RapidsOperator op, RapidsVariable? other);
    public abstract string VariableTypeName { get; }
    public abstract bool Truthy { get; }
    public abstract RapidsVariable? GetMember(string memberName);
    public abstract RapidsVariable ShallowCopy();

    public static RapidsVariable FromJSON(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new RapidsNumberVariable(element.GetDouble());
            case JsonValueKind.String:
                return new RapidsStringVariable(element.GetString() ?? "");
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new RapidsNullVariable();
            case JsonValueKind.Object:
                var obj = new RapidsObjectVariable();
                
                foreach (var property in element.EnumerateObject())
                {
                    obj.ObjectValues[property.Name] = FromJSON(property.Value);
                }

                return obj;
            case JsonValueKind.Array:
                var list = new RapidsListVariable();

                foreach (var item in element.EnumerateArray())
                {
                    list.List.Add(FromJSON(item));
                }

                return list;
            case JsonValueKind.True:
                return new RapidsBooleanVariable(true);
            case JsonValueKind.False:
                return new RapidsBooleanVariable(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(element));
        }
    }
}