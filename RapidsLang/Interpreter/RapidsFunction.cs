namespace RapidsLang.Interpreter;

public class RapidsFunction(Func<InterpreterContext, RapidsFunctionResult> func)
{
    public Func<InterpreterContext, RapidsFunctionResult> Function { get; private init; } = func;
}

public record RapidsFunctionResult(
    bool ReturnedAnything,
    RapidsRuntimeError? RapidsRuntimeError
)
{
    public static RapidsFunctionResult VoidRet() => new(false, null);
    public static RapidsFunctionResult Returned() => new(true, null);
    public static RapidsFunctionResult Err(string message) => new(false, new(message));
}

public record RapidsRuntimeError(string Message);