using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class StringsModule : Module
{
    public void CharFromCode(InterpreterContext ctx)
    {
        if (!ctx.FunctionCallStack.TryPop(out var variable) || variable is not RapidsNumberVariable charCode)
        {
            // todo error
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        
        ctx.FunctionCallStack.Push(new RapidsStringVariable(Convert.ToChar((byte) charCode.Value).ToString()));
    }

    public void CodeFromChar(InterpreterContext ctx)
    {
        if (!ctx.FunctionCallStack.TryPop(out var variable) || variable is not RapidsStringVariable str)
        {
            // todo error
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        
        ctx.FunctionCallStack.Push(new RapidsNumberVariable(str.Value[0]));
    }
    
    public override void Import(InterpreterContext context)
    {
        context.AddNativeFunction("charFromCode", CharFromCode);
        context.AddNativeFunction("codeFromChar", CodeFromChar);
    }
}