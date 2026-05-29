using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM;

public class Frame(int localCount)
{
    public RapidsVariable[] Locals = new RapidsVariable[localCount];
    public Stack<RapidsVariable> Stack = [];
    public int Pc;
}