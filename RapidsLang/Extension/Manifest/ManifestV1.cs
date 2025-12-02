using System.Text.Json.Serialization;
using RapidsLang.Extension.Communication;
using RapidsLang.Extensions.Manifest;

namespace RapidsLang.Extension.Manifest;

public class ManifestV1 : BaseManifest
{
    [JsonPropertyName("module_name")] 
    public required string ModuleName { get; set; }
    
    [JsonPropertyName("module_version")] 
    public required string ModuleVersion { get; set; }
    
    [JsonPropertyName("submodules")] 
    public List<string>? Submodules { get; set; } 
    
    [JsonPropertyName("main")] 
    public string? MainSourcePath { get; set; }

    [JsonPropertyName("protocol")]
    public CommunicationProtocol? Protocol { get; set; }

    public override ExtensionManifest MigrateToLatest()
    {
        return new ExtensionManifest
        {
            ModuleName = ModuleName,
            ModuleVersion = ModuleVersion,
            Submodules = Submodules,
            MainSourcePath = MainSourcePath,
            Protocol = Protocol
        };
    }
}