namespace RapidsLang.InterpreterVM;

public class RapidProgram
{
    public BytecodeHeader Header;
    public OpCode[] Code;

    public byte[] ToBytes()
    {
        List<byte> bytes = [];
        
        bytes.AddRange(Header.ToBytes());
        bytes.AddRange(Code.Select(c => c.ToBytes()).SelectMany(b => b));

        return bytes.ToArray();
    }

    public static RapidProgram FromBytes(ReadOnlySpan<byte> data)
    {
        var header = BytecodeHeader.FromBytes(data);
    
        // Skip past the header using the total size baked into it
        var headerSize = BitConverter.ToInt32(data.Slice(BytecodeHeader.Signature.Length + 4, 4));
        var codeSpan = data[headerSize..];
    
        var opcodes = new List<OpCode>();
        var offset = 0;
    
        while (offset < codeSpan.Length)
        {
            var opBytes = codeSpan[offset..];
            var op = OpCode.FromBytes(opBytes.ToArray()); // ToArray until FromBytes takes a span
            opcodes.Add(op);
            offset += op.Size;
        }
    
        return new RapidProgram
        {
            Header = header,
            Code = opcodes.ToArray()
        };
    }
}