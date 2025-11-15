using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Types;

namespace RapidsLang.Interpreter.Lib.Modules;

public class ArraysModule : Module
{
    private static void MakeArrayOfSizeWithValue(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        if (!ctx.FunctionCallStack.TryPop(out var sizeVal))
        {
            return;
        }

        if (sizeVal is not RapidsNumberVariable size)
        {
            return;
        }

        if (!ctx.FunctionCallStack.TryPop(out var initialValue))
        {
            return;
        }

        var list = new RapidsListVariable();
        for (int i = 0; i < size.Value; i++)
        {
            list.List.Add(initialValue.ShallowCopy());
        }
        
        ctx.FunctionCallStack.Push(list);
    }

    private static readonly RapidsType MakeArrayOfSizeWithValueType = new RapidsFunctionType(
        [RapidsAnyType.Instance],
        RapidsAnyType.Instance
    );

    public override ModuleExports Exports { get; } = new (new Dictionary<string, ModuleExport> {
        {"filledArray", new(RapidsFunctionReferenceVariable.OfNative(
                MakeArrayOfSizeWithValue,
                MakeArrayOfSizeWithValueType
            ), MakeArrayOfSizeWithValueType)
        }
    });
}