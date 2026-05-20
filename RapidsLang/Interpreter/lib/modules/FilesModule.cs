using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class FilesModule: Module
{
    public override ModuleExports Exports => new(new()
    {
        {"writeFile", new(RapidsFunctionReferenceVariable.OfNative(WriteFile), WriteFileType)}
    });
    
    private static readonly RapidsType WriteFileType = new RapidsFunctionType(
        [new("path", RapidsStringType.Instance),
        new("content", RapidsStringType.Instance)],
        null
    );

    private void WriteFile(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil();

        var content = util.LatestString();
        var path = util.LatestString();

        if (path is null)
        {
            Console.WriteLine("Failed to write to file because path was null.");
            return;
        }
        
        File.WriteAllText(path.Value, content?.Value ?? "");
    }
}