using RapidsLang.NativeExtension.Variables;

namespace RapidsLang.NativeExtension;

public static class ArgumentUtils
{
    public static bool? GetBoolean(this List<ExtensionVariable> instance, int index)
    {
        var exVar = instance.ElementAtOrDefault(index);

        if (exVar is ExtensionBooleanVariable exBool)
        {
            return exBool.Value;
        }

        return null;
    }
    
    public static double? GetDouble(this List<ExtensionVariable> instance, int index)
    {
        var exVar = instance.ElementAtOrDefault(index);

        if (exVar is ExtensionNumberVariable exNum)
        {
            return exNum.Value;
        }

        return null;
    }
    
    public static string? GetString(this List<ExtensionVariable> instance, int index)
    {
        var exVar = instance.ElementAtOrDefault(index);

        if (exVar is ExtensionStringVariable exStr)
        {
            return exStr.Value;
        }

        return null;
    }
    
    public static ExtensionObjectVariable? GetObject(this List<ExtensionVariable> instance, int index)
    {
        var exVar = instance.ElementAtOrDefault(index);

        if (exVar is ExtensionObjectVariable exObj)
        {
            return exObj;
        }

        return null;
    }
    
    public static List<ExtensionVariable>? GetList(this List<ExtensionVariable> instance, int index)
    {
        var exVar = instance.ElementAtOrDefault(index);

        if (exVar is ExtensionListVariable exList)
        {
            return exList.Value;
        }

        return null;
    }
}