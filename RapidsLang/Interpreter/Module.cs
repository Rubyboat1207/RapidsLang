namespace RapidsLang.Interpreter;

public abstract class Module
{
    public abstract void Import(InterpreterContext context);
}