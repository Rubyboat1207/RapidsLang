using System.Text.Json;
using RapidsLang.Extensions.Manifest;

namespace RapidsLang.Extensions;

public static class ExtensionLoader
{
    public static List<ManifestContainer> GetExtensionManifests()
    {
        List<ManifestContainer> list = [];
        var folderPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "extensions/");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        var directoryContents = Directory.GetDirectories(folderPath);

        foreach (var directory in directoryContents)
        {
            var files = Directory.GetFiles(directory);

            if (files.Any(f => Path.GetFileName(f) == "manifest.rpd.json"))
            {
                var text = Path.Join(directory, "manifest.rpd.json");

                try
                {
                    var manifest = JsonSerializer.Deserialize<BaseManifest>(text);

                    if (manifest is null)
                    {
                        continue;
                    }
                    
                    list.Add(new ManifestContainer(manifest.MigrateToLatest(), directory));
                }
                catch
                {
                    // ignored
                }
            }

        }

        return list;
    }
}

public record ManifestContainer(
    ExtensionManifest ExtensionManifest,
    string DirectoryPath
);