namespace RapidsLang.InterpreterVM;

public struct RapidProgram
{
    public BytecodeHeader Header;
    public OpCode[] Code;
}