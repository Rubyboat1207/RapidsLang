using RapidsLang.Extensions.Manifest;

namespace RapidsLang.Extensions;

public record ExtensionData(
    ExtensionManifest ExtensionManifest,
    string DirectoryPath
)
{
    private string? _mainCode = null;
    public string MainCodePath = Path.Join(DirectoryPath, ExtensionManifest.MainSourcePath ?? "main.rpd");

    public string GetMainCodeString()
    {
        if (_mainCode is not null)
        {
            return _mainCode;
        }
        var path = MainCodePath;

        _mainCode = File.ReadAllText(path);

        return _mainCode;
    }
}