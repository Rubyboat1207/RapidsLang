namespace RapidsLang.NativeExtension;

public record Identifier(
    string NameSpace,
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