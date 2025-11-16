namespace RapidsLang.Parser.Types;

public class RapidsFunctionType(List<RapidsType> parameterTypes, RapidsType? returnType) : RapidsType
{
    public List<RapidsType> ParameterTypes { get; } = parameterTypes;
    public RapidsType? ReturnType { get; } = returnType;
    public override string Name => $"({string.Join(", ", ParameterTypes.Select(p => p.Name))}): {ReturnType?.Name ?? "void"}>";

    public override Dictionary<string, RapidsType> GetMembers() => [];

    public override RapidsType? GetMember(string memberName)
    {
        return null;
    }

    public override bool IsSameType(RapidsType other)
    {
        if (other == AnyFunctionType)
        {
            return true;
        }

        // ReSharper disable once InvertIf
        if (other is RapidsFunctionType rapidsFunctionType)
        {
            if (rapidsFunctionType.ReturnType is not null == ReturnType is not null)
            {
                return false;
            }

            if (rapidsFunctionType.ReturnType is not null && ReturnType is not null)
            {
                if (!rapidsFunctionType.ReturnType.IsSameType(ReturnType))
                {
                    return false;
                }
            }
            
            for (var i = 0; i < ParameterTypes.Count; i++)
            {
                var param = ParameterTypes[i];
                var otherParam = rapidsFunctionType.ParameterTypes.ElementAtOrDefault(i);

                if (otherParam != null)
                {
                    if (!param.IsSameType(otherParam))
                    {
                        return false;
                    } 
                }
                else
                {
                    return false;
                }
            }
        }
        
        return base.IsSameType(other);
    }

    public static readonly RapidsFunctionType AnyFunctionType = new([], RapidsAnyType.Instance);
}