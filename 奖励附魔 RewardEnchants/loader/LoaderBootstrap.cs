using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace RewardEnchants.Loader;

[ModInitializer(nameof(Initialize))]
public static class LoaderBootstrap
{
	private const string ModId = "RewardEnchants";
	private const string RealDllName = "RewardEnchants.dll";
	private const string VariantManifestName = "reward-enchants-variants.manifest";
	private const string CompatTargetMetadataKey = "RewardEnchantsCompatibilityTarget";
	private static readonly List<Assembly> VariantAssemblies = [];
	private static readonly MethodInfo? AssociateMethod = typeof(ModManager).GetMethod(
		"AssociateAssemblyWithMod",
		BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
		binder: null,
		[typeof(string), typeof(Assembly)],
		modifiers: null);
	private static readonly FieldInfo? AssembliesField = typeof(Mod).GetField(
		"assemblies",
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly FieldInfo? LegacyAssemblyField = typeof(Mod).GetField(
		"assembly",
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static Assembly? _selectedAssembly;
	private static bool _bridgeInstalled;
	private static bool _legacyCallbackInstalled;

	public static void Initialize()
	{
		string? directory = Path.GetDirectoryName(typeof(LoaderBootstrap).Assembly.Location);
		if (string.IsNullOrWhiteSpace(directory))
		{
			Log.Error("[RewardEnchants.Loader] Could not resolve loader directory.");
			return;
		}

		Version? host = ResolveHostVersion();
		Candidate? candidate = ReadCandidates(directory)
			.Where(item => host == null || item.Version <= host)
			.OrderBy(item => item.Version)
			.LastOrDefault();
		if (candidate == null)
		{
			Log.Error($"[RewardEnchants.Loader] No compatible variant for host {host?.ToString() ?? "unknown"}.");
			return;
		}

		Log.Info($"[RewardEnchants.Loader] Host version {host?.ToString() ?? "unknown"}; picked variant {candidate.Target}.");
		try
		{
			AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(typeof(LoaderBootstrap).Assembly) ?? AssemblyLoadContext.Default;
			Assembly assembly = context.LoadFromAssemblyPath(candidate.Path);
			ValidateAssembly(assembly, candidate.Target);
			RegisterAssembly(assembly);
			InvokeInitializer(assembly);
		}
		catch (Exception exception)
		{
			Log.Error($"[RewardEnchants.Loader] Failed to initialize {candidate.Path}: {exception}");
		}
	}

	private static List<Candidate> ReadCandidates(string directory)
	{
		string manifestPath = Path.Combine(directory, VariantManifestName);
		if (!File.Exists(manifestPath))
		{
			Log.Error($"[RewardEnchants.Loader] Missing variant manifest: {manifestPath}");
			return [];
		}

		BundleManifest? manifest;
		try
		{
			manifest = JsonSerializer.Deserialize<BundleManifest>(
				File.ReadAllText(manifestPath),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		}
		catch (Exception exception)
		{
			Log.Error($"[RewardEnchants.Loader] Invalid variant manifest: {exception.Message}");
			return [];
		}

		if (manifest?.Schema != 1 || manifest.Variants == null || manifest.Variants.Count == 0)
		{
			Log.Error("[RewardEnchants.Loader] Variant manifest has no usable entries.");
			return [];
		}

		string libRoot = Path.GetFullPath(Path.Combine(directory, "lib"));
		var targets = new HashSet<string>(StringComparer.Ordinal);
		var candidates = new List<Candidate>();
		foreach (BundleEntry entry in manifest.Variants)
		{
			string target = entry.CompatTarget?.Trim() ?? string.Empty;
			if (!Version.TryParse(target, out Version? version)
				|| !targets.Add(target)
				|| entry.Assembly != RealDllName
				|| string.IsNullOrWhiteSpace(entry.Directory))
			{
				Log.Error("[RewardEnchants.Loader] Variant manifest contains an invalid entry.");
				return [];
			}

			string variantDirectory = Path.GetFullPath(Path.Combine(directory, entry.Directory));
			if (!IsUnder(variantDirectory, libRoot)
				|| Path.GetFileName(variantDirectory) != target)
			{
				Log.Error($"[RewardEnchants.Loader] Unsafe variant directory for {target}.");
				return [];
			}

			string marker = Path.Combine(variantDirectory, "compat-target.txt");
			string dll = Path.Combine(variantDirectory, RealDllName);
			if (!File.Exists(marker)
				|| File.ReadAllText(marker).Trim() != target
				|| !File.Exists(dll)
				|| !HashMatches(dll, entry.Sha256))
			{
				Log.Error($"[RewardEnchants.Loader] Integrity validation failed for {target}.");
				return [];
			}
			candidates.Add(new Candidate(target, version, dll));
		}
		return candidates;
	}

	private static bool IsUnder(string path, string root)
	{
		string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		return normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
	}

	private static bool HashMatches(string path, string? expected)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return false;
		}
		using FileStream stream = File.OpenRead(path);
		return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expected, StringComparison.OrdinalIgnoreCase);
	}

	private static void ValidateAssembly(Assembly assembly, string target)
	{
		if (assembly.GetName().Name != ModId)
		{
			throw new BadImageFormatException($"Unexpected assembly identity {assembly.GetName().Name}.");
		}
		string? embeddedTarget = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(item => item.Key == CompatTargetMetadataKey)?.Value;
		if (embeddedTarget != target)
		{
			throw new BadImageFormatException($"Variant target {embeddedTarget ?? "missing"} does not match {target}.");
		}
	}

	private static void RegisterAssembly(Assembly assembly)
	{
		_selectedAssembly = assembly;
		VariantAssemblies.Add(assembly);
		if (!_bridgeInstalled)
		{
			MethodInfo getter = AccessTools.PropertyGetter(typeof(ReflectionHelper), nameof(ReflectionHelper.ModTypes))
				?? throw new MissingMethodException("ReflectionHelper.ModTypes");
			new Harmony("Natsuki.RewardEnchants.Loader.ReflectionBridge").Patch(
				getter,
				postfix: new HarmonyMethod(typeof(LoaderBootstrap), nameof(ModTypesPostfix)));
			_bridgeInstalled = true;
		}

		if (AssociateMethod != null)
		{
			try
			{
				AssociateMethod.Invoke(null, [ModId, assembly]);
			}
			catch (Exception exception)
			{
				Log.Warn($"[RewardEnchants.Loader] Direct assembly association failed: {exception.GetBaseException().Message}");
			}
		}

		if (TryFindMod(out Mod? mod)
			&& AssembliesField?.GetValue(mod) is IList assemblies
			&& !assemblies.Cast<object>().Any(item => ReferenceEquals(item, assembly)))
		{
			assemblies.Add(assembly);
		}

		if (LegacyAssemblyField != null && !_legacyCallbackInstalled)
		{
			ModManager.OnModDetected += OnLegacyModDetected;
			_legacyCallbackInstalled = true;
		}
	}

	private static void ModTypesPostfix(ref Type[] __result)
	{
		__result = __result.Concat(VariantAssemblies.SelectMany(GetLoadableTypes)).Distinct().ToArray();
	}

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			return exception.Types.OfType<Type>();
		}
	}

	private static void OnLegacyModDetected(Mod mod)
	{
		if (_selectedAssembly == null || ReadManifestId(mod) != ModId)
		{
			return;
		}
		LegacyAssemblyField?.SetValue(mod, _selectedAssembly);
		ModManager.OnModDetected -= OnLegacyModDetected;
		_legacyCallbackInstalled = false;
	}

	private static bool TryFindMod(out Mod? mod)
	{
		mod = ModManager.Mods.FirstOrDefault(candidate => ReadManifestId(candidate) == ModId);
		return mod != null;
	}

	private static string? ReadManifestId(Mod mod)
	{
		object? manifest = typeof(Mod).GetField(
			"manifest",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(mod);
		return manifest?.GetType().GetField(
			"id",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(manifest) as string;
	}

	private static void InvokeInitializer(Assembly assembly)
	{
		foreach (Type type in GetLoadableTypes(assembly))
		{
			ModInitializerAttribute? attribute = type.GetCustomAttribute<ModInitializerAttribute>();
			if (attribute == null)
			{
				continue;
			}
			MethodInfo method = type.GetMethod(
				attribute.initializerMethod,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				?? throw new MissingMethodException(type.FullName, attribute.initializerMethod);
			method.Invoke(null, null);
			return;
		}
		throw new MissingMethodException("Implementation initializer not found.");
	}

	private static Version? ResolveHostVersion()
	{
		try
		{
			if (TryParseVersion(ReleaseInfoManager.Instance.ReleaseInfo?.Version, out Version version))
			{
				return version;
			}
		}
		catch
		{
		}

		string? executable = TryCallGodot("GetExecutablePath");
		if (!string.IsNullOrWhiteSpace(executable))
		{
			foreach (string path in new[]
			{
				Path.Combine(Path.GetDirectoryName(executable)!, "..", "Resources", "release_info.json"),
				Path.Combine(Path.GetDirectoryName(executable)!, "release_info.json")
			})
			{
				try
				{
					using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
					if (TryParseVersion(document.RootElement.GetProperty("version").GetString(), out Version version))
					{
						return version;
					}
				}
				catch
				{
				}
			}
		}
		return null;
	}

	private static string? TryCallGodot(string name)
	{
		try
		{
			Type? type = Type.GetType("Godot.OS, GodotSharp", throwOnError: false);
			return type?.GetMethod(name, BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null) as string;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryParseVersion(string? label, out Version version)
	{
		string value = label?.Trim() ?? string.Empty;
		int suffix = value.IndexOfAny(['-', '+']);
		if (suffix >= 0)
		{
			value = value[..suffix];
		}
		if (value.StartsWith('v') || value.StartsWith('V'))
		{
			value = value[1..];
		}
		return Version.TryParse(value, out version!);
	}

	private sealed record Candidate(string Target, Version Version, string Path);

	private sealed class BundleManifest
	{
		public int Schema { get; set; }
		public List<BundleEntry>? Variants { get; set; }
	}

	private sealed class BundleEntry
	{
		public string? CompatTarget { get; set; }
		public string? Directory { get; set; }
		public string? Assembly { get; set; }
		public string? Sha256 { get; set; }
	}
}
