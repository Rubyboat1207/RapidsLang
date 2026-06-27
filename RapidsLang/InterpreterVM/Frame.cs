using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM;

public class Frame(uint localCount, int? functionIndex=null)
{
    public VariableHolder?[] Locals = new VariableHolder[localCount];
    public Stack<RapidsVariable> Stack = [];
    public int Pc;
    public int? FunctionIndex = functionIndex;
}