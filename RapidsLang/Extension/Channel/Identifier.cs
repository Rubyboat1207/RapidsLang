using System.Text.Json.Serialization;

namespace RapidsLang.Extensions.Channel;

public record Identifier(
    [property: JsonPropertyName("namespace")]
    string NameSpace,
    [property: JsonPropertyName("path")]
    string Path
)
{
    public virtual bool Equals(Identifier? other)
    {
        if (other == null)
        {
            return false;
        }
        return other.Path == Path && other.NameSpace == NameSpace;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(NameSpace, Path);
    }
}