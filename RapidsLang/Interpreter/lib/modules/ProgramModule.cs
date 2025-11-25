using RapidsLang.Analyzer.Types;
using RapidsLang.Extension.Channel;
using RapidsLang.Extension.Communication;
using RapidsLang.Extension.Communication.Native;
using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Lib.Modules;

public class ProgramModule : Module
{
    private NativeProtocol? _protocol;
    public override CommunicationProtocol? Protocol => _protocol;
    public static Identifier SigintIdent = new("program", "sigint");

    private ModuleExports? _exports;

    public override ModuleExports Exports => _exports ?? new(new() {
        // for Linting
        { "sigint", new(null, new RapidsChannelSourceType(RapidsPrimitiveType.Bool, "critical")) }
    });

    public override void Import(RapidsInterpreter interpreter, List<ImportNode>? importNodes)
    {
        _protocol = interpreter.NativeProtocol;
        
        _exports = new ModuleExports(new()
        {
            {"sigint", new ModuleExport(
                new RapidsDataChannelVariable(
                    new(_protocol, SigintIdent, true, false),
                    this
                ),
                new RapidsChannelSourceType(new RapidsAnyType(), null)
            )}
        });
        
        base.Import(interpreter, importNodes);
    }
    
    
}