namespace RapidsLang.InterpreterVM;

public class RapidsProgramFunctionBlock
{
    public RapidsBytecodeFunction[] Functions;

    public byte[] ToBytes()
    {
        MemoryStream bytes = new();
        bytes.Write(new byte[8]);
        bytes.Write(BitConverter.GetBytes((uint) Functions.Length));
        foreach (var function in Functions)
        {
            bytes.Write(function.ToBytes());
        }

        var finalBytes = bytes.ToArray();
        BitConverter.GetBytes((ulong)bytes.Length).CopyTo(finalBytes, 0);

        return finalBytes;
    }

    public static RapidsProgramFunctionBlock FromBytes(byte[] data)
    {
        var offset = 8;
        
        var length = BitConverter.ToUInt32(data[offset..]); offset += 4;
        
        var functions = new RapidsBytecodeFunction[length];
            
        for (var i = 0ul; i < length; i++)
        {
            functions[i] = RapidsBytecodeFunction.FromBytes(data[offset..], out var fnOffset);
            offset += fnOffset;
        }

        return new RapidsProgramFunctionBlock { Functions = functions };
    }
}

public record RapidsBytecodeFunction(OpCode[] Code, uint ParameterCount, uint LocalCount)
{
    public byte[] ToBytes()
    {
        MemoryStream bytes = new();
        
        bytes.Write(BitConverter.GetBytes((uint) Code.Length));
        bytes.Write(BitConverter.GetBytes(LocalCount));
        bytes.Write(BitConverter.GetBytes(ParameterCount));
        bytes.Write(Code.Select(f => f.ToBytes()).SelectMany(f => f).ToArray());
        

        return bytes.ToArray();
    }

    public static RapidsBytecodeFunction FromBytes(byte[] bytes, out int offset)
    {
        offset = 0;
        var funcLength = BitConverter.ToUInt32(bytes[offset..]); offset += 4;
        var localCount = BitConverter.ToUInt32(bytes[offset..]); offset += 4;
        var paramCount = BitConverter.ToUInt32(bytes[offset..]); offset += 4;
        var code = new OpCode[funcLength];

        for (var j = 0ul; j < funcLength; j++)
        {
            var opBytes = OpCode.FromBytes(bytes[offset..]);
            code[j] = opBytes;
            offset += opBytes.Size;
        }

        return new RapidsBytecodeFunction(code, paramCount, localCount);
    }
}