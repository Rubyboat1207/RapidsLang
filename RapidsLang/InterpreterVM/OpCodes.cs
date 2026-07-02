using System.Reflection;

namespace RapidsLang.InterpreterVM;

public record OpCode
{
    public byte Code
    {
        get
        {
            return this switch
            {
                Add                    => 1,
                Subtract               => 2,
                Multiply               => 3,
                Divide                 => 4,
                Modulo                 => 5,
                Index                  => 6,
                GreaterThan            => 7,
                LessThan               => 8,
                GreaterThanEqualto     => 9,
                LessThanEqualto        => 10,
                Equal                  => 11,
                Not                    => 12,
                Truthy                 => 13,
                MemberAccess           => 14,
                Jump                   => 15,
                JumpIfTrue             => 16,
                JumpIfFalse            => 17,
                Return                 => 18,
                Call                   => 19,
                LoadLocal              => 20,
                StoreLocal             => 21,
                LoadGlobal             => 22,
                LoadString             => 23,
                Concat                 => 24,
                LoadNumber             => 25,
                CaptureFunctionClosure => 26,
                PushFrame              => 27,
                PopFrame               => 28,
                LoadBool               => 29,
                LoadFunction           => 30,
                JumpRel                => 31,
                JumpIfTrueRel          => 32,
                JumpIfFalseRel         => 33,
                AssembleList           => 34,
                GetIterator            => 35,
                IteratorNext           => 36,
                PushIteratorKey        => 37,
                PushIteratorValue      => 38,
                GetMember              => 39,
                And                    => 40,
                Or                     => 41,
                IteratorComplete       => 42,
                Exit                   => 255,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public virtual byte[] ToBytes()
    {
        return [Code];
    }

    public static OpCode FromBytes(byte[] bytes)
    {
        return bytes[0] switch
        {
            1   => new Add(),
            2   => new Subtract(),
            3   => new Multiply(),
            4   => new Divide(),
            5   => new Modulo(),
            6   => new Index(),
            7   => new GreaterThan(),
            8   => new LessThan(),
            9   => new GreaterThanEqualto(),
            10  => new LessThanEqualto(),
            11  => new Equal(),
            12  => new Not(),
            13  => new Truthy(),
            14  => new MemberAccess(),
            15  => new Jump(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            16  => new JumpIfTrue(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            17  => new JumpIfFalse(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            18  => new Return(),
            19  => new Call(),
            20  => new LoadLocal(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            21  => new StoreLocal(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            22  => new LoadGlobal(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            23  => new LoadString(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            24  => new Concat(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            25  => new LoadNumber(BitConverter.ToDouble(bytes.AsSpan(1, sizeof(double)))),
            26  => new CaptureFunctionClosure(),
            27  => new PushFrame(),
            28  => new PopFrame(),
            29  => new LoadBool(bytes[1] == 1),
            30  => new LoadFunction(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            31  => new JumpRel(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            32  => new JumpIfTrueRel(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            33  => new JumpIfFalseRel(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            34  => new AssembleList(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            35  => new GetIterator(),
            36  => new IteratorNext(),
            37  => new PushIteratorKey(),
            38  => new PushIteratorValue(),
            39  => new GetMember(BitConverter.ToInt32(bytes.AsSpan(1, 4))),
            40  => new And(),
            41  => new Or(),
            42  => new IteratorComplete(),
            255 => new Exit(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public virtual int Size => 1;

    public virtual string AsString()
    {
        return GetType().Name;
    }
}

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

public record SingleArgOp(int Value) : OpCode {
    public override byte[] ToBytes()
    {
        var bytes = new byte[1 + sizeof(int)];
        bytes[0] = Code;
        BitConverter.GetBytes(Value).CopyTo(bytes, 1);
        return bytes;
    }
    
    public override int Size => 1 + sizeof(int);

    public override string AsString()
    {
        return base.AsString() + " " + Value;
    }
}

public record SingleArgByteOp(byte Value) : OpCode {
    public override byte[] ToBytes()
    {
        var bytes = new byte[2];
        bytes[0] = Code;
        bytes[1] = Value;
        return bytes;
    }
    
    public override int Size => 2;

    public override string AsString()
    {
        return base.AsString() + " " + Value;
    }
}

public record SingleArgNumberOp(double Value) : OpCode {
    public override byte[] ToBytes()
    {
        var bytes = new byte[1 + sizeof(double)];
        bytes[0] = Code;
        BitConverter.GetBytes(Value).CopyTo(bytes, 1);
        return bytes;
    }
    
    public override int Size => 1 + sizeof(double);
    
    public override string AsString()
    {
        return base.AsString() + " " + Value;
    }
}

public record Jump(int Value) : SingleArgOp(Value);
public record JumpIfTrue(int Value) : SingleArgOp(Value);
public record JumpIfFalse(int Value) : SingleArgOp(Value);

public record JumpRel(int Value) : SingleArgOp(Value);
public record JumpIfFalseRel(int Value) : SingleArgOp(Value);
public record JumpIfTrueRel(int Value) : SingleArgOp(Value);

public record Call : OpCode;
public record Return : OpCode;

public record LoadLocal(int Value) : SingleArgOp(Value);
public record StoreLocal(int Value) : SingleArgOp(Value);
public record LoadGlobal(int Value) : SingleArgOp(Value);


public record LoadString(int Value) : SingleArgOp(Value);
public record Concat(int Value) : SingleArgOp(Value);

public record LoadNumber(double Value) : SingleArgNumberOp(Value);

public record CaptureFunctionClosure : OpCode;

public record PushFrame : OpCode;
public record PopFrame : OpCode;
public record LoadFunction(int Value) : SingleArgOp(Value);

public record LoadBool(bool Bool) : SingleArgByteOp(Bool ? (byte)1 : (byte)0)
{
    public override string AsString()
    {
        return "LoadBool " + Bool;
    }
}

public record AssembleList(int Value) : SingleArgOp(Value);
public record GetIterator : OpCode;

public record IteratorNext : OpCode;

public record PushIteratorValue : OpCode;

public record PushIteratorKey : OpCode;
public record GetMember(int Value) : SingleArgOp(Value);

public record And : OpCode;
public record Or : OpCode;

public record IteratorComplete : OpCode;

public record Exit : OpCode;
public record NoOp : OpCode;