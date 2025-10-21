using System.Text.Json.Serialization;

namespace RapidsLang.Extensions.Manifest;

// Note for maintainers:
// the latest manifest version will always have the class name ExtensionManifest
// When you make a new manifest version, rename the old one to ManifestV<number>
// And rename your current manifest to be ExtensionManifest

[JsonPolymorphic(TypeDiscriminatorPropertyName = "manifest_version")]
[JsonDerivedType(typeof(ExtensionManifest), 1)]
public abstract class BaseManifest
{
    [JsonPropertyName("manifest_version")]
    public required int ManifestVersion { get; set; }

    public abstract ExtensionManifest MigrateToLatest();
}
