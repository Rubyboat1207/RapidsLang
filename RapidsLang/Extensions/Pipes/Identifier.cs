namespace RapidsLang.Extensions.Pipes;

public record Identifier(
    string NameSpace,
    string Value
)
{
    public virtual bool Equals(Identifier? other)
    {
        if (other == null)
        {
            return false;
        }
        return other.Value == Value && other.NameSpace == NameSpace;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(NameSpace, Value);
    }
}