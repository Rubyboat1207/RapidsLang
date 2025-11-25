using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class StringsModule : Module
{
    private static void CharFromCode(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        if (!ctx.FunctionCallStack.TryPop(out var variable) || variable is not RapidsNumberVariable charCode)
        {
            // todo error
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        
        ctx.FunctionCallStack.Push(new RapidsStringVariable(Convert.ToChar((byte) charCode.Value).ToString()));
    }

    private static readonly RapidsType CharFromCodeType = new RapidsFunctionType(
        [new("value", RapidsPrimitiveType.Number)],
        RapidsStringType.Instance
    );

    private static void CodeFromChar(RapidsInterpreter interpreter)
    {
        var ctx = interpreter.Context;
        if (!ctx.FunctionCallStack.TryPop(out var variable) || variable is not RapidsStringVariable str)
        {
            // todo error
            ctx.FunctionCallStack.Push(new RapidsNullVariable());
            return;
        }
        
        ctx.FunctionCallStack.Push(new RapidsNumberVariable(str.Value[0]));
    }
    private static readonly RapidsType CodeFromCharType = new RapidsFunctionType(
        [new("value", RapidsStringType.Instance)],
        RapidsPrimitiveType.Number
    );

    public override ModuleExports Exports { get; } = new ModuleExports(new Dictionary<string, ModuleExport> {
        {"charFromCode", new(RapidsFunctionReferenceVariable.OfNative(CharFromCode, CharFromCodeType), CharFromCodeType)},
        {"codeFromChar", new(RapidsFunctionReferenceVariable.OfNative(CodeFromChar, CodeFromCharType), CodeFromCharType)},
    });
}