using System.Text;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.InterpreterVM;

public class BytecodeHeader
{
    public int Version;
    public required ModuleImport[] Modules;
    public required string[] Strings;
    public uint GlobalsCount;
    public uint OutermostLocalsCount;
    public static readonly byte[] Signature = "RPD<3 "u8.ToArray();
    public static readonly int CurrentVersion = 0;
    
    // Assumes little-endian. I do not care about BE systems.
    public byte[] ToBytes()
    {
        MemoryStream bytes = new();
        bytes.Write(Signature);
        bytes.Write(BitConverter.GetBytes(Version));
        var totalSizeIndex = bytes.Length;
        bytes.Write(new byte[8]); // total size of the header
        
        bytes.Write(BitConverter.GetBytes(GlobalsCount));
        bytes.Write(BitConverter.GetBytes(OutermostLocalsCount));
        bytes.Write(BitConverter.GetBytes(Modules.Length));
        bytes.Write(BitConverter.GetBytes(Strings.Length));
        
        foreach (var module in Modules)
        {
            var modNameBytes = Encoding.UTF8.GetBytes(module.ModuleName);
            bytes.Write(BitConverter.GetBytes(modNameBytes.Length));
            bytes.Write(modNameBytes);
            
            bytes.Write(BitConverter.GetBytes(module.Imports.Length));
            foreach (var import in module.Imports)
            {
                var importBytes = Encoding.UTF8.GetBytes(import);
                bytes.Write(BitConverter.GetBytes(importBytes.Length));
                bytes.Write(importBytes);
            }
        }
        
        foreach (var str in Strings)
        {
            var strBytes = Encoding.UTF8.GetBytes(str);
            bytes.Write(BitConverter.GetBytes(strBytes.Length));
            bytes.Write(strBytes);
        }

        var finalBytes = bytes.ToArray();

        {
            var bs = BitConverter.GetBytes(finalBytes.Length);
            for (var i = 0; i < bs.Length; i++)
            {
                var b = bs[i];
                finalBytes[totalSizeIndex + i] = b;
            }
        }

        return finalBytes;
    }
    
    public static BytecodeHeader FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < Signature.Length)
            throw new InvalidDataException("Data too short to contain signature.");
        
        if (!data[..Signature.Length].SequenceEqual(Signature))
            throw new InvalidDataException("Invalid signature.");
        
        var offset = Signature.Length;
        
        var version = BitConverter.ToInt32(data[offset..]); offset += 4;
        
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported bytecode version {version}.");
        
        var totalSize = BitConverter.ToInt32(data[offset..]); offset += 8;
        
        if (data.Length < totalSize)
            throw new InvalidDataException("Data truncated — expected {totalSize} bytes, got {data.Length}.");
        
        var globalsCount = BitConverter.ToUInt32(data[offset..]); offset += 4;
        var outermostLocalsCount = BitConverter.ToUInt32(data[offset..]); offset += 4;
        var moduleCount = BitConverter.ToInt32(data[offset..]); offset += 4;
        var stringCount = BitConverter.ToInt32(data[offset..]); offset += 4;
        
        var modules = new ModuleImport[moduleCount];
        for (var i = 0; i < moduleCount; i++)
        {
            var nameLen = BitConverter.ToInt32(data[offset..]); offset += 4;
            var moduleName = Encoding.UTF8.GetString(data.Slice(offset, nameLen)); offset += nameLen;
            
            var importCount = BitConverter.ToInt32(data[offset..]); offset += 4;
            var imports = new string[importCount];
            for (var j = 0; j < importCount; j++)
            {
                var importLen = BitConverter.ToInt32(data[offset..]); offset += 4;
                imports[j] = Encoding.UTF8.GetString(data.Slice(offset, importLen)); offset += importLen;
            }
            
            modules[i] = new ModuleImport(moduleName, imports);
        }
        
        var strings = new string[stringCount];
        for (var i = 0; i < stringCount; i++)
        {
            var strLen = BitConverter.ToInt32(data[offset..]); offset += 4;
            strings[i] = Encoding.UTF8.GetString(data.Slice(offset, strLen)); offset += strLen;
        }
        
        return new BytecodeHeader
        {
            Version = version,
            GlobalsCount = globalsCount,
            OutermostLocalsCount = outermostLocalsCount,
            Modules = modules,
            Strings = strings
        };
    }
}