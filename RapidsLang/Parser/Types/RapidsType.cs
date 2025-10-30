namespace RapidsLang.Parser.Types;

public abstract class RapidsType
{
    public abstract string Name { get; }

    public virtual bool IsSameType(RapidsType other) => Name != other.Name;
}


public class RapidsPrimitiveType(string name) : RapidsType
{
    public override string Name { get; } = name;
    
    // Singleton instances for common primitives
    public static readonly RapidsPrimitiveType Number = new("number");
    public static readonly RapidsPrimitiveType String = new("string");
    public static readonly RapidsPrimitiveType Bool = new("boolean");
    public static readonly RapidsPrimitiveType Null = new("null");
}

public class RapidsAnyType : RapidsType
{
    public override string Name => "any";
    public override bool IsSameType(RapidsType other) => true;

    public static RapidsAnyType Instance { get; } = new();
}

public class RapidsArrayType(RapidsType elementType) : RapidsType
{
    public RapidsType ElementType { get; } = elementType;
    public override string Name => $"{ElementType.Name}[]";
}

public class RapidsUnionType : RapidsType
{
    public HashSet<RapidsType> Types { get; }

    public RapidsUnionType(HashSet<RapidsType> types)
    {
        Types = new HashSet<RapidsType>();
        foreach (var type in types)
        {
            if (type is RapidsUnionType nestedUnion)
            {
                Types.UnionWith(nestedUnion.Types);
            }
            else
            {
                Types.Add(type);
            }
        }
    }

    public override string Name
    {
        get
        {
            var sortedNames = Types.Select(t => t.Name).OrderBy(name => name);
            return string.Join(" | ", sortedNames);
        }
    }

    public override bool IsSameType(RapidsType other)
    {
        if (other is not RapidsUnionType otherUnion)
        {
            return false;
        }

        return Types.SetEquals(otherUnion.Types);
    }
}

public class RapidsFunctionType(List<RapidsType> parameterTypes, RapidsType? returnType) : RapidsType
{
    public List<RapidsType> ParameterTypes { get; } = parameterTypes;
    public RapidsType? ReturnType { get; } = returnType;
    public override string Name => $"({string.Join(", ", ParameterTypes.Select(p => p.Name))}) => {ReturnType?.Name ?? "void"}";
}

public class RapidsShapeType(Dictionary<string, RapidsType> properties) : RapidsType
{
    public Dictionary<string, RapidsType> Properties { get; } = properties;

    public override string Name
    {
        get
        {
            var props = Properties.Select(kvp => $"{kvp.Key}: {kvp.Value.Name}");
            return $"{{ {string.Join(", ", props)} }}";
        }
    }
}

public class RapidsDictionaryType(RapidsType valueType) : RapidsType
{
    public RapidsType ValueType { get; } = valueType;

    public override string Name => $"{{ [string]: {ValueType.Name} }}";
    
    public static readonly RapidsDictionaryType Unparameterized = new(RapidsAnyType.Instance);
}

public class RapidsChannelSourceType(RapidsType valueType) : RapidsType
{
    public RapidsType ValueType { get; } = valueType;
    public override string Name => $"-^{ValueType.Name}";
}

public class RapidsChannelTargetType(RapidsType valueType) : RapidsType
{
    public RapidsType ValueType { get; } = valueType;
    public override string Name => $"{ValueType.Name}-^";
}

public class RapidsBiDirectionalChannelType(RapidsChannelSourceType source, RapidsChannelTargetType target) : RapidsType
{
    public RapidsChannelSourceType Source { get; } = source;
    public RapidsChannelTargetType Target { get; } = target;
    public override string Name => $"({Source}&{Target})";
}