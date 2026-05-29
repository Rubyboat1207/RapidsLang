namespace RapidsLang.InterpreterVM;

public record OpCode;

public record Add : OpCode;
public record Subtract : OpCode;
public record Multiply : OpCode;
public record Divide : OpCode;
public record Modulo : OpCode;
public record Index : OpCode;

public record GreaterThan : OpCode;
public record LessThan : OpCode;
public record GreaterThanEqualto : OpCode;
public record LessThanEqualto : OpCode;
public record Equal : OpCode;
public record Not : OpCode;
public record Truthy : OpCode;

public record MemberAccess : OpCode;

public record Jump(int Index) : OpCode;
public record JumpIfTrue(int Index) : OpCode;
public record JumpIfFalse(int Index) : OpCode;
public record Call : OpCode;
public record Return : OpCode;

public record LoadLocal(int Index) : OpCode;
public record StoreLocal(int Index) : OpCode;
public record LoadGlobal(int Index) : OpCode;


public record LoadString(int Index) : OpCode;
public record Concat(int Count) : OpCode;

public record LoadNumber(double Value) : OpCode;

public record LoadFunction(int FunctionIndex) : OpCode;