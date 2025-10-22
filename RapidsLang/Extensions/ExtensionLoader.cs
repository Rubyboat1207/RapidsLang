using System.Text.Json;
using RapidsLang.Extensions.Manifest;

namespace RapidsLang.Extensions;

public static class ExtensionLoader
{
    public static List<ExtensionData> GetExternalExtensions()
    {
        List<ExtensionData> list = [];
        var folderPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rapids/extensions/");
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
                var text = File.ReadAllText(Path.Join(directory, "manifest.rpd.json"));

                try
                {
                    var manifest = JsonSerializer.Deserialize<BaseManifest>(text);

                    if (manifest is null)
                    {
                        continue;
                    }
                    
                    list.Add(new ExtensionData(manifest.MigrateToLatest(), directory));
                }
                catch(Exception e)
                {
                    throw new Exception($"Failed parse manifest for module at {directory}", e);
                }
            }

        }

        return list;
    }
}