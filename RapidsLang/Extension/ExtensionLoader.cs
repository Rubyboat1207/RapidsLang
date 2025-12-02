using System.Text.Json;
using RapidsLang.Extensions;
using RapidsLang.Extensions.Manifest;

namespace RapidsLang.Extension;

public static class ExtensionLoader
{
    public static readonly string[] LanguageFeatureSets = ["core"];
    
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

                    var data = new ExtensionData(manifest.MigrateToLatest(), directory);

                    var notIncludedFeatureSets =
                        (data.ExtensionManifest.RequiredFeatureSets ?? ["core"]).Where(s =>
                            !LanguageFeatureSets.Contains(s)).ToList();
                    
                    if (notIncludedFeatureSets.Count > 0)
                    {
                        Console.WriteLine($"" +
                          $"[WARNING] Extension module requires feature(s) that are not currently supported. Will cause issues. " +
                          $"\nRequires:" +
                          $"\n{string.Join(",\n", data.ExtensionManifest.RequiredFeatureSets ?? ["core"])}" +
                          $"\nbut current interpreter features only includes: " +
                          $"\n{string.Join(",\n", LanguageFeatureSets)}" +
                          $"\n Missing: " +
                          $"{string.Join(",\n", notIncludedFeatureSets)}");
                    }
                    
                    list.Add(data);
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