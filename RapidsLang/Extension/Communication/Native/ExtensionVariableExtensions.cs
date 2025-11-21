using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;
using RapidsLang.NativeExtension.Variables;

namespace RapidsLang.Extension.Communication.Native;

public static class ExtensionVariableExtensions
{
    public static RapidsBooleanVariable ToRapidsVariable(this ExtensionBooleanVariable instance)
    {
        return new(instance.Value);
    }

    public static RapidsNullVariable ToRapidsVariable(this ExtensionNullVariable instance)
    {
        return new();
    }

    public static RapidsNumberVariable ToRapidsVariable(this ExtensionNumberVariable instance)
    {
        return new(instance.Value);
    }
    
    public static RapidsStringVariable ToRapidsVariable(this ExtensionStringVariable instance)
    {
        return new(instance.Value);
    }
    
    public static RapidsListVariable ToRapidsVariable(this ExtensionListVariable instance)
    {
        return new(instance.Value.Select(v => v.ToRapidsVariable()).ToList());
    }
    
    public static RapidsObjectVariable ToRapidsVariable(this ExtensionObjectVariable instance)
    {
        return new(instance.Value.Select(v => (v.Key, v.Value.ToRapidsVariable())).ToDictionary());
    }

    public static RapidsFunctionReferenceVariable ToRapidsVariable(this ExtensionFunctionVariable instance)
    {
        return RapidsFunctionReferenceVariable.OfNative((interpreter) =>
        {
            List<ExtensionVariable> variables = [];
            for (var i = 0; i < instance.ExpectedParams; i++)
            {
                variables.Insert(0, interpreter.Context.FunctionCallStack.Pop().ToExtensionVariable());
            }
            
            var res = instance.ExtensionFunction.Invoke(variables);

            if (res is not null)
            {
                interpreter.Context.FunctionCallStack.Push(res.ToRapidsVariable());
            }
        });
    }

    public static DataChannel ToRapidsDataChannel(
        this ExtensionDataChannel instance,
        NativeProtocol protocol
    )
    {
        return new(
            protocol,
            instance.Identifier.ToRapidsIdentifier(),
            instance.Readable,
            instance.Writable,
            instance.DataVariableName
        );
    }
    
    public static RapidsVariable ToRapidsVariable(this ExtensionVariable instance)
    {
        switch (instance)
        {
            case ExtensionBooleanVariable b: return b.ToRapidsVariable();
            case ExtensionListVariable lst: return lst.ToRapidsVariable();
            case ExtensionNullVariable nul: return nul.ToRapidsVariable();
            case ExtensionNumberVariable num: return num.ToRapidsVariable();
            case ExtensionObjectVariable obj: return obj.ToRapidsVariable();
            case ExtensionStringVariable str: return str.ToRapidsVariable();
            case ExtensionFunctionVariable fun: return fun.ToRapidsVariable();
            case ExtensionDataChannel: return new RapidsNullVariable();
        }

        throw new ArgumentOutOfRangeException();
    }
    
    public static RapidsVariable ToRapidsVariableWithModule(this ExtensionVariable instance, Module module, NativeProtocol protocol)
    {
        switch (instance)
        {
            case ExtensionDataChannel chn: return new RapidsDataChannelVariable(
                chn.ToRapidsDataChannel(protocol),
                module
            );
            default: return instance.ToRapidsVariable();
        }
    }
}