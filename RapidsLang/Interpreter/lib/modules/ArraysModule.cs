using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class ArraysModule : Module
{
    public void MakeArrayOfSizeWithValue(InterpreterContext ctx)
    {
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
    
    public override void Import(InterpreterContext context)
    {
        context.AddNativeFunction("filledArray", MakeArrayOfSizeWithValue);
    }
}