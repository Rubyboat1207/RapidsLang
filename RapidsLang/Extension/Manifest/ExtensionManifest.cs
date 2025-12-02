using System.Text.Json.Serialization;
using RapidsLang.Extensions.Manifest;

namespace RapidsLang.Extension.Manifest;

public class ExtensionManifest : ManifestV1
{
    [JsonPropertyName("required_feature_sets")]
    public string[]? RequiredFeatureSets;
    public override ExtensionManifest MigrateToLatest()
    {
        return this;
    }
}