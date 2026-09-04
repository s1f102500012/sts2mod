using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace BetterCharacterRelics.Loader;

[ModInitializer(nameof(Initialize))]
public static class LoaderBootstrap
{
    private const string ModId = "BetterCharacterRelics";
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        string? selectedPath = null;
        try
        {
            string directory = Path.GetDirectoryName(typeof(LoaderBootstrap).Assembly.Location)!;
            Version? host = ResolveHostVersion();
            string target = SelectTarget(host);
            Log.Info($"[{ModId}.Loader] Host={host?.ToString() ?? "unknown"}; selected={target}.");
            if (host == null || host.ToString() != target)
                Log.Warn($"[{ModId}.Loader] Host is outside the verified targets 0.107.1 / 0.111.0; using {target} as best effort.");
            selectedPath = ResolveVariant(directory, target);
            var context = AssemblyLoadContext.GetLoadContext(typeof(LoaderBootstrap).Assembly) ?? AssemblyLoadContext.Default;
            Assembly implementation = context.LoadFromAssemblyPath(selectedPath);
            if (implementation.GetName().Name != ModId || implementation.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(item => item.Key == "CompatibilityTarget").Value != target)
                throw new InvalidOperationException("Variant assembly identity or target does not match its directory");

            // 0.111.0 有公开关联 API；0.107.1 没有。此模组不声明模型或存档字段，
            // 因而旧版无需改 Mod.assembly，更不能给 ReflectionHelper.ModTypes 加全局桥。
            MethodInfo? associate = typeof(ModManager).GetMethod("AssociateAssemblyWithMod",
                BindingFlags.Public | BindingFlags.Static, null, [typeof(string), typeof(Assembly)], null);
            if (associate != null)
            {
                associate.Invoke(null, [ModId, implementation]);
                Log.Info($"[{ModId}.Loader] Associated implementation through public ModManager API.");
            }
            else Log.Info($"[{ModId}.Loader] Legacy host: model-free implementation; no discovery bridge required.");

            var type = implementation.GetTypes().Single(type => type.IsDefined(typeof(ModInitializerAttribute)));
            string methodName = type.GetCustomAttribute<ModInitializerAttribute>()!.initializerMethod;
            MethodInfo initializer = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
                ?? throw new MissingMethodException(type.FullName, methodName);
            initializer.Invoke(null, null);
            _initialized = true;
        }
        catch (Exception exception)
        {
            Log.Error($"[{ModId}.Loader] Failed to initialize {selectedPath ?? "bundle"}: {exception}");
            throw;
        }
    }

    internal static string SelectTarget(Version? host)
    {
        if (host == null || host >= new Version(0, 111, 0)) return "0.111.0";
        if (host >= new Version(0, 107, 1)) return "0.107.1";
        throw new NotSupportedException($"Unsupported host {host}");
    }

    internal static string ResolveVariant(string directory, string target)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "better-character-relics-variants.manifest")));
        if (document.RootElement.GetProperty("schema").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported variant manifest schema");
        var entry = document.RootElement.GetProperty("variants").EnumerateArray()
            .Single(entry => entry.GetProperty("compatTarget").GetString() == target);
        string expectedDirectory = Path.GetFullPath(Path.Combine(directory, "lib", target));
        string resolved = Path.GetFullPath(Path.Combine(directory, entry.GetProperty("directory").GetString()!));
        if (resolved != expectedDirectory || entry.GetProperty("assembly").GetString() != ModId + ".dll")
            throw new InvalidOperationException("Unsafe variant path");
        if (File.ReadAllText(Path.Combine(resolved, "compat-target.txt")).Trim() != target)
            throw new InvalidOperationException("Variant compatibility marker mismatch");
        // SHA 清单只供构建/交付校验，不在玩家启动时验证“官方构建”指纹。
        string assemblyPath = Path.Combine(resolved, ModId + ".dll");
        if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Variant missing", assemblyPath);
        return assemblyPath;
    }

    internal static Version? ParseVersion(string? label)
    {
        string text = (label ?? "").Trim().TrimStart('v', 'V').Split(['-', '+'])[0];
        return Version.TryParse(text, out var result) ? result : null;
    }

    private static Version? ResolveHostVersion()
    {
        try
        {
            Version? version = ParseVersion(ReleaseInfoManager.Instance.ReleaseInfo?.Version);
            if (version != null) return version;
        }
        catch (Exception exception)
        {
            Log.Info($"[{ModId}.Loader] ReleaseInfo unavailable: {exception.GetBaseException().Message}");
        }
        string executableDirectory = Path.GetDirectoryName(Godot.OS.GetExecutablePath())!;
        foreach (string path in new[] { Path.Combine(executableDirectory, "..", "Resources", "release_info.json"), Path.Combine(executableDirectory, "release_info.json") })
        {
            if (!File.Exists(path)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (ParseVersion(document.RootElement.GetProperty("version").GetString()) is Version version) return version;
        }
        return null;
    }
}
