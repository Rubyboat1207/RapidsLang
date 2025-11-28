using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;

namespace RapidsLang.Extension.Communication.Native;

public class ExtensionLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    // Expose the actual path we resolved (e.g. with .dll appended)
    // so the loader knows exactly which file to load.
    public string PluginPath { get; }

    /// <summary>
    /// Initializes the context.
    /// </summary>
    /// <param name="pluginPath">Relative or Absolute path to the plugin.</param>
    /// <param name="rootDirectory">
    /// Optional. If provided, relative 'pluginPath' values are resolved against this directory 
    /// instead of the Current Working Directory.
    /// </param>
    public ExtensionLoadContext(string pluginPath, string? rootDirectory = null) : base(isCollectible: true)
    {
        string candidatePath;

        // 0. Resolve the initial candidate path based on rootDirectory
        if (Path.IsPathRooted(pluginPath))
        {
            // Absolute paths ignore the rootDirectory
            candidatePath = pluginPath;
        }
        else if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            // Resolve relative path against the custom root
            candidatePath = Path.Combine(rootDirectory, pluginPath);
        }
        else
        {
            // Resolve relative path against CWD (default behavior)
            candidatePath = Path.GetFullPath(pluginPath);
        }

        // Normalize the path (fix slashes, resolve '..', etc.)
        candidatePath = Path.GetFullPath(candidatePath);

        string? foundPath = null;

        // 1. Directory check: look for "Folder/Folder.dll"
        if (Directory.Exists(candidatePath))
        {
            string folderName = new DirectoryInfo(candidatePath).Name;
            string internalDll = Path.Combine(candidatePath, folderName + ".dll");
            if (File.Exists(internalDll))
            {
                foundPath = internalDll;
            }
        }
        // 2. Direct file check
        else if (File.Exists(candidatePath))
        {
             foundPath = candidatePath;
        }
        // 3. Missing extension check
        else 
        {
            if (!candidatePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string withExtension = candidatePath + ".dll";
                if (File.Exists(withExtension))
                {
                    foundPath = withExtension;
                }
            }
        }

        if (foundPath == null)
        {
            string attemptedDirDll = Directory.Exists(candidatePath) 
                ? Path.Combine(candidatePath, new DirectoryInfo(candidatePath).Name + ".dll") 
                : "(Not a directory)";
                
            string msg = $"""
                Plugin assembly not found. 
                Input Path: {pluginPath}
                Root Dir: {rootDirectory ?? "CWD"}
                Resolved Path: {candidatePath}
                
                Probed Locations:
                1. As File: {(File.Exists(candidatePath) ? "Found" : "Not Found")}
                2. As Folder (looking for internal DLL): {attemptedDirDll}
                3. As File + .dll: {candidatePath}.dll
                """;
                
            throw new FileNotFoundException(msg);
        }

        PluginPath = foundPath;
        _pluginDirectory = Path.GetDirectoryName(foundPath) ?? string.Empty;
        
        // CRITICAL: This ensures dependencies are loaded relative to the DLL, not the CWD.
        _resolver = new AssemblyDependencyResolver(foundPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Step 1: Shared host deps
        try
        {
            var defaultAssembly = Default.LoadFromAssemblyName(assemblyName);
            if (defaultAssembly != null)
                return defaultAssembly;
        }
        catch
        {
            // Ignore
        }
        
        var existing = Assemblies.FirstOrDefault(x => x.GetName().Name == assemblyName.Name);
        if (existing != null)
        {
            return existing;
        }
        
        // Step 2: Ask resolver (.deps.json)
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        
        // Step 3: Runtime Directory Fallback
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrEmpty(runtimeDirectory))
        {
            var runtimePath = Path.Combine(runtimeDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(runtimePath))
            {
                return LoadFromAssemblyPath(runtimePath);
            }
        }
        
        // Step 4: Manual Fallback (look in plugin folder)
        var localPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(localPath))
        {
             return LoadFromAssemblyPath(localPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // 1. Ask the resolver
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        // 2. Manual check in plugin folder
        string[] searchNames = GetNativeSearchNames(unmanagedDllName);
        
        foreach (var name in searchNames)
        {
            string manualPath = Path.Combine(_pluginDirectory, name);
            if (File.Exists(manualPath))
            {
                return LoadUnmanagedDllFromPath(manualPath);
            }
        }

        return base.LoadUnmanagedDll(unmanagedDllName);
    }

    private static string[] GetNativeSearchNames(string baseName)
    {
        if (OperatingSystem.IsWindows()) return [baseName, baseName + ".dll"];
        if (OperatingSystem.IsLinux())   return [baseName, baseName + ".so", "lib" + baseName + ".so", "lib" + baseName];
        if (OperatingSystem.IsMacOS())   return [baseName, baseName + ".dylib", "lib" + baseName + ".dylib", "lib" + baseName];
        return [baseName];
    }
}