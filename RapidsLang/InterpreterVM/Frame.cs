using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM;

public class Frame(uint localCount)
{
    public RapidsVariable[] Locals = new RapidsVariable[localCount];
    public Stack<RapidsVariable> Stack = [];
    public int Pc;
}