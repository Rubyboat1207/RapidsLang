using System.Text;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM;

public class RapidProgram
{
    public BytecodeHeader Header;
    public OpCode[] Code;
    public RapidsProgramFunctionBlock FunctionBlock;

    public byte[] ToBytes()
    {
        List<byte> bytes = [];
        
        bytes.AddRange(Header.ToBytes());
        bytes.AddRange(FunctionBlock.ToBytes());
        bytes.AddRange(Code.Select(c => c.ToBytes()).SelectMany(b => b));

        return bytes.ToArray();
    }

    public static RapidProgram FromBytes(ReadOnlySpan<byte> data)
    {
        var header = BytecodeHeader.FromBytes(data);
    
        // Skip past the header using the total size baked into it
        var headerSize = BitConverter.ToInt32(data.Slice(BytecodeHeader.Signature.Length + 4, 8));
        var functionsSize = BitConverter.ToInt32(data.Slice(headerSize, 8));

        var functionSpan = data[headerSize..(headerSize + functionsSize)].ToArray();
        
        var codeSpan = data[(headerSize + functionsSize)..];
    
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
            FunctionBlock = RapidsProgramFunctionBlock.FromBytes(functionSpan),
            Code = opcodes.ToArray()
        };
    }

    public string Disassemble()
    {
        var sw = new StringBuilder();
        
        sw.Append("RAPIDS BYTECODE\n\nVERSION: ");
        sw.AppendLine(Header.Version.ToString());
        sw.Append("GLOBALS COUNT:");
        sw.AppendLine(Header.GlobalsCount.ToString());
        sw.Append("OUTERMOST LOCALS COUNT:");
        sw.AppendLine(Header.OutermostLocalsCount.ToString());
        sw.AppendLine("\nMODULES:");
        
        var moduleRegistry = new ModuleRegistry();
        List<(string, ModuleExport)> exported = [];
        foreach (var importedModule in Header.Modules)
        {
            if (!moduleRegistry.TryGetModule(importedModule.ModuleName, out var module)) continue;

            exported.AddRange(importedModule.Imports.Select(import => (import, module.Exports.Exports[import])));
        }
        
        foreach (var headerModule in Header.Modules)
        {
            sw.AppendLine(headerModule.ModuleName);
            
            if (headerModule.Imports.Length <= 0) continue;
            
            foreach (var import in headerModule.Imports)
            {
                sw.Append("  ");
                sw.AppendLine(import);
            }
        }
        
        sw.AppendLine("\nSTRINGS:");
        for (var i = 0; i < Header.Strings.Length; i++)
        {
            var headerString = Header.Strings[i];
            sw.Append(i);
            sw.Append(": `");
            sw.Append(headerString);
            sw.AppendLine("`");
        }

        sw.AppendLine("\nFUNCTIONS:");
        for (var i = 0; i < FunctionBlock.Functions.Length; i++)
        {
            var func = FunctionBlock.Functions[i];

            sw.AppendLine($"FUNC {i} - Params: {func.ParameterCount} | Locals: {func.LocalCount}");

            WriteOpcodes(func.Code, exported, sw);
        }


        sw.AppendLine("\nCODE:");
        WriteOpcodes(Code, exported, sw);

        return sw.ToString();
    }

    public void WriteOpcodes(OpCode[] opcodes, List<(string, ModuleExport)> exported, StringBuilder sw)
    {
        Dictionary<int, List<int>> JumpLookup = [];
        // Build any helpful stuff.
        for (var i = 0; i < opcodes.Length; i++)
        {
            var opcode = opcodes[i];

            // ReSharper disable once InvertIf
            if (opcode is SingleArgOp j and (Jump or JumpIfFalse or JumpIfTrue))
            {
                if (JumpLookup.TryGetValue(j.Value, out var fromList))
                {
                    fromList.Add(i);
                }
                else
                {
                    JumpLookup[j.Value] = [i];
                }
            }
        }
        
        for (var i = 0; i < opcodes.Length; i++)
        {
            var opcode = opcodes[i];

            if (JumpLookup.TryGetValue(i, out var fromList))
            {
                foreach (var from in fromList)
                {
                    sw.Append('j');
                    sw.Append(from);
                    sw.AppendLine(": ");
                }
            }

            sw.Append(i);
            sw.Append(" - ");
            
            sw.Append(opcode.AsString());
            if (opcode is LoadGlobal lg)
            {
                sw.Append("; ");
                sw.Append(exported[lg.Value].Item1);
            }

            if (opcode is LoadString ls)
            {
                sw.Append("; `");
                sw.Append(Header.Strings[ls.Value].ReplaceLineEndings("\\n"));
                sw.Append('`');
            }

            sw.Append('\n');

        }
    }
}