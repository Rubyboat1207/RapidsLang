namespace RapidsLang.Utils;

public static class Env
{
    public static void Load(string filePath = ".env")
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Warning: '{filePath}' not found. Skipping environment load.");
            return;
        }

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            var parts = trimmedLine.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (value.Length > 1 && 
                ((value.StartsWith("\"") && value.EndsWith("\"")) || 
                 (value.StartsWith("'") && value.EndsWith("'"))))
            {
                value = value.Substring(1, value.Length - 2);
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}