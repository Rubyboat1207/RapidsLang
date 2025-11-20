using RapidsLang.Interpreter.Variables;
using RapidsLang.NativeExtension.Variables;

namespace RapidsLang.Extension.Communication.Native;

public static class RapidsVariableExtensions
{
    public static ExtensionBooleanVariable ToExtensionVariable(this RapidsBooleanVariable instance)
    {
        return new(instance.Value);
    }

    public static ExtensionNullVariable ToExtensionVariable(this RapidsNullVariable instance)
    {
        return new();
    }

    public static ExtensionNumberVariable ToExtensionVariable(this RapidsNumberVariable instance)
    {
        return new(instance.Value);
    }
    
    public static ExtensionStringVariable ToExtensionVariable(this RapidsStringVariable instance)
    {
        return new(instance.Value);
    }
    
    public static ExtensionListVariable ToExtensionVariable(this RapidsListVariable instance)
    {
        return new(instance.List.Select(v => v.ToExtensionVariable()).ToList());
    }
    
    public static ExtensionObjectVariable ToExtensionVariable(this RapidsObjectVariable instance)
    {
        return new(instance.ObjectValues.Select(v => (v.Key, v.Value.ToExtensionVariable())).ToDictionary());
    }
    
    public static ExtensionVariable ToExtensionVariable(this RapidsVariable instance)
    {
        switch (instance)
        {
            case RapidsBooleanVariable b: return b.ToExtensionVariable();
            case RapidsListVariable lst: return lst.ToExtensionVariable();
            case RapidsNullVariable nul: return nul.ToExtensionVariable();
            case RapidsNumberVariable num: return num.ToExtensionVariable();
            case RapidsObjectVariable obj: return obj.ToExtensionVariable();
            case RapidsStringVariable str: return str.ToExtensionVariable();
        }

        throw new ArgumentOutOfRangeException();
    }
}