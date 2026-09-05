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

namespace UniversalDominionSword.Loader;

[ModInitializer(nameof(Initialize))]
public static class LoaderBootstrap
{
	private const string ModId = "UniversalDominionSword";
	private const string RealDllName = "UniversalDominionSword.dll";
	private const string VariantManifestName = "universal-dominion-sword-variants.manifest";
	private const string CompatTargetMarkerName = "compat-target.txt";
	private const string CompatTargetMetadataKey = "UniversalDominionSwordCompatibilityTarget";

	private static readonly object VariantAssembliesGate = new();
	private static readonly List<Assembly> VariantAssemblies = [];

	private static readonly MethodInfo? AssociateAssemblyWithModMethod =
		typeof(ModManager).GetMethod(
			"AssociateAssemblyWithMod",
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
			binder: null,
			[typeof(string), typeof(Assembly)],
			modifiers: null);

	private static readonly FieldInfo? ModAssembliesField =
		typeof(Mod).GetField(
			"assemblies",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly FieldInfo? LegacyModAssemblyField =
		typeof(Mod).GetField(
			"assembly",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static Assembly? _selectedVariantAssembly;
	private static bool _reflectionBridgeInstalled;
	private static bool _legacyAssociationCallbackInstalled;

	public static void Initialize()
	{
		LinuxNativeDependencyBootstrap.EnsureHarmonyRuntimeDependenciesVisible();

		string? loaderDirectory = Path.GetDirectoryName(typeof(LoaderBootstrap).Assembly.Location);
		if (string.IsNullOrWhiteSpace(loaderDirectory))
		{
			Log.Error("[UniversalDominionSword.Loader] Could not resolve loader directory.");
			return;
		}

		string libRoot = Path.Combine(loaderDirectory, "lib");
		if (!Directory.Exists(libRoot))
		{
			Log.Error($"[UniversalDominionSword.Loader] Missing lib directory: {libRoot}");
			return;
		}

		HostVersionSnapshot host = ResolveHostVersion();
		VariantCandidate? variant = PickVariant(loaderDirectory, libRoot, host.Numeric);
		if (variant == null)
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] No valid variant under {libRoot} " +
				$"for host {host.ReleaseLabel ?? host.Numeric?.ToString() ?? "unknown"}.");
			return;
		}

		Log.Info(
			$"[UniversalDominionSword.Loader] Host version label={host.ReleaseLabel ?? "<none>"} " +
			$"numeric={host.Numeric?.ToString() ?? "<none>"}; picked variant {variant.CompatTarget}.");

		try
		{
			AssemblyLoadContext context =
				AssemblyLoadContext.GetLoadContext(typeof(LoaderBootstrap).Assembly)
				?? AssemblyLoadContext.Default;
			Assembly realAssembly = context.LoadFromAssemblyPath(variant.DllPath);
			ValidateVariantAssembly(realAssembly, variant);

			RegisterVariantAssembly(realAssembly);
			AssociateVariantAssemblyWithGame(realAssembly);
			InvokeRealInitializer(realAssembly);
		}
		catch (Exception exception)
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Failed to load or initialize " +
				$"{variant.DllPath}: {exception}");
		}
	}

	private static void ValidateVariantAssembly(
		Assembly assembly,
		VariantCandidate variant)
	{
		if (!string.Equals(
			assembly.GetName().Name,
			Path.GetFileNameWithoutExtension(RealDllName),
			StringComparison.Ordinal))
		{
			throw new BadImageFormatException(
				$"Variant assembly identity is {assembly.GetName().Name}, expected UniversalDominionSword.");
		}

		string? embeddedTarget = assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(attribute =>
				string.Equals(
					attribute.Key,
					CompatTargetMetadataKey,
					StringComparison.Ordinal))
			?.Value;
		if (!string.Equals(
			embeddedTarget,
			variant.CompatTarget,
			StringComparison.Ordinal))
		{
			throw new BadImageFormatException(
				$"Variant compatibility metadata is {embeddedTarget ?? "<missing>"}, " +
				$"expected {variant.CompatTarget}.");
		}
	}

	private static void RegisterVariantAssembly(Assembly assembly)
	{
		lock (VariantAssembliesGate)
		{
			if (!VariantAssemblies.Any(candidate =>
				string.Equals(candidate.Location, assembly.Location, StringComparison.Ordinal)))
			{
				VariantAssemblies.Add(assembly);
			}
		}
	}

	private static void InstallReflectionBridge()
	{
		if (_reflectionBridgeInstalled)
		{
			return;
		}

		MethodInfo? getter =
			AccessTools.PropertyGetter(typeof(ReflectionHelper), nameof(ReflectionHelper.ModTypes));
		if (getter == null)
		{
			throw new MissingMethodException("ReflectionHelper.ModTypes getter was not found.");
		}

		new Harmony("Natsuki.UniversalDominionSword.Loader.ReflectionBridge").Patch(
			getter,
			postfix: new HarmonyMethod(
				typeof(LoaderBootstrap),
				nameof(ReflectionHelperModTypesPostfix)));
		_reflectionBridgeInstalled = true;
	}

	private static void ReflectionHelperModTypesPostfix(ref Type[] __result)
	{
		Type[] variantTypes;
		lock (VariantAssembliesGate)
		{
			variantTypes = VariantAssemblies
				.SelectMany(GetLoadableTypes)
				.Distinct()
				.ToArray();
		}

		if (variantTypes.Length > 0)
		{
			__result = __result.Concat(variantTypes).Distinct().ToArray();
		}
	}

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			Log.Warn(
				$"[UniversalDominionSword.Loader] Partial type load for " +
				$"{assembly.FullName}: {exception.Message}");
			return exception.Types.OfType<Type>();
		}
	}

	// 三级回退,且只走一条:
	//   ① 0.108+ ModManager.AssociateAssemblyWithMod → 游戏自己把变体程序集并入 Mod.assemblies,类型发现随之生效;
	//   ② 反射向 Mod.assemblies 追加;
	//   ③ 0.107.1 只有单个 Mod.assembly 字段,且 ModManager 在初始化器返回后会用 loader 覆盖它,
	//      只能在 OnModDetected 里替换,并补 ReflectionHelper.ModTypes 后缀让类型发现看到变体。
	// ①② 成功后绝不再装 ReflectionHelper.ModTypes 后缀:两条路径同时贡献类型会让 ModelDb 把同一个类型
	// 看成两个,进而触发 "Two AbstractModels X and X share an ID" 的自比较告警。
	private static void AssociateVariantAssemblyWithGame(Assembly assembly)
	{
		_selectedVariantAssembly = assembly;

		if (AssociateAssemblyWithModMethod != null)
		{
			try
			{
				AssociateAssemblyWithModMethod.Invoke(null, [ModId, assembly]);
				if (IsAssemblyAssociatedWithMod(assembly))
				{
					Log.Info("[UniversalDominionSword.Loader] Variant associated via ModManager.AssociateAssemblyWithMod.");
					return;
				}
			}
			catch (Exception exception)
			{
				Log.Warn(
					$"[UniversalDominionSword.Loader] AssociateAssemblyWithMod failed: " +
					$"{exception.GetBaseException().Message}");
			}
		}

		if (TryAssociateWithAssemblyList(assembly))
		{
			Log.Info("[UniversalDominionSword.Loader] Variant associated by appending to Mod.assemblies.");
			return;
		}

		InstallReflectionBridge();
		if (LegacyModAssemblyField != null && !_legacyAssociationCallbackInstalled)
		{
			ModManager.OnModDetected += OnLegacyModDetected;
			_legacyAssociationCallbackInstalled = true;
			return;
		}

		Log.Warn(
			"[UniversalDominionSword.Loader] Could not associate the selected variant " +
			"with ModManager; type discovery will rely on the reflection bridge.");
	}

	private static void OnLegacyModDetected(Mod mod)
	{
		if (_selectedVariantAssembly == null
			|| !string.Equals(ReadManifestId(mod), ModId, StringComparison.Ordinal))
		{
			return;
		}

		LegacyModAssemblyField?.SetValue(mod, _selectedVariantAssembly);
		ModManager.OnModDetected -= OnLegacyModDetected;
		_legacyAssociationCallbackInstalled = false;
		Log.Info(
			$"[UniversalDominionSword.Loader] Associated variant " +
			$"{_selectedVariantAssembly.GetName().Name} with the STS2 0.107.x mod record.");
	}

	private static bool TryAssociateWithAssemblyList(Assembly assembly)
	{
		if (!TryFindMod(out Mod? mod)
			|| ModAssembliesField?.GetValue(mod) is not IList assemblies)
		{
			return false;
		}

		if (!assemblies.Cast<object>().Any(item => ReferenceEquals(item, assembly)))
		{
			assemblies.Add(assembly);
		}
		return true;
	}

	private static bool IsAssemblyAssociatedWithMod(Assembly assembly)
	{
		return TryFindMod(out Mod? mod)
			&& ModAssembliesField?.GetValue(mod) is IList assemblies
			&& assemblies.Cast<object>().Any(item => ReferenceEquals(item, assembly));
	}

	private static bool TryFindMod(out Mod? mod)
	{
		mod = ModManager.Mods.FirstOrDefault(candidate =>
			string.Equals(ReadManifestId(candidate), ModId, StringComparison.Ordinal));
		return mod != null;
	}

	private static string? ReadManifestId(Mod mod)
	{
		FieldInfo? manifestField = typeof(Mod).GetField(
			"manifest",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		object? manifest = manifestField?.GetValue(mod);
		FieldInfo? idField = manifest?.GetType().GetField(
			"id",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		return idField?.GetValue(manifest) as string;
	}

	private static void InvokeRealInitializer(Assembly assembly)
	{
		foreach (Type type in GetLoadableTypes(assembly))
		{
			ModInitializerAttribute? attribute =
				type.GetCustomAttribute<ModInitializerAttribute>();
			if (attribute == null)
			{
				continue;
			}

			MethodInfo? initializer = type.GetMethod(
				attribute.initializerMethod,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (initializer == null)
			{
				throw new MissingMethodException(
					$"{type.FullName}.{attribute.initializerMethod} was not found.");
			}

			initializer.Invoke(null, null);
			return;
		}

		throw new MissingMethodException(
			$"No {nameof(ModInitializerAttribute)} was found in {assembly.FullName}.");
	}

	private static VariantCandidate? PickVariant(
		string loaderDirectory,
		string libRoot,
		Version? host)
	{
		List<VariantCandidate> variants =
			LoadVariantManifest(loaderDirectory, libRoot)
				.OrderBy(candidate => candidate.Version)
				.ToList();
		if (variants.Count == 0)
		{
			return null;
		}

		if (host == null)
		{
			Log.Warn(
				"[UniversalDominionSword.Loader] Host version is unknown; " +
				"using the newest bundled variant.");
			return variants[^1];
		}

		return variants.LastOrDefault(candidate => candidate.Version <= host)
			?? variants[^1];
	}

	private static List<VariantCandidate> LoadVariantManifest(
		string loaderDirectory,
		string libRoot)
	{
		string path = Path.Combine(loaderDirectory, VariantManifestName);
		if (!File.Exists(path))
		{
			Log.Error($"[UniversalDominionSword.Loader] Missing variant manifest: {path}");
			return [];
		}

		BundleVariantManifest? manifest;
		try
		{
			manifest = JsonSerializer.Deserialize<BundleVariantManifest>(
				File.ReadAllText(path),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		}
		catch (Exception exception)
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Failed to read variant manifest: {exception}");
			return [];
		}

		if (manifest?.Variants == null || manifest.Variants.Count == 0)
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Variant manifest contains no variants: {path}");
			return [];
		}

		string fullLibRoot = Path.GetFullPath(libRoot);
		return manifest.Variants
			.Select(entry => TryCreateVariantCandidate(
				loaderDirectory,
				fullLibRoot,
				entry))
			.OfType<VariantCandidate>()
			.ToList();
	}

	private static VariantCandidate? TryCreateVariantCandidate(
		string loaderDirectory,
		string fullLibRoot,
		BundleVariantEntry entry)
	{
		string compatTarget = entry.CompatTarget?.Trim() ?? string.Empty;
		if (!TryParseVersion(compatTarget, out Version version))
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Ignoring invalid target " +
				$"'{entry.CompatTarget}'.");
			return null;
		}

		string relativeDirectory = string.IsNullOrWhiteSpace(entry.Directory)
			? Path.Combine("lib", compatTarget)
			: entry.Directory.Trim();
		string variantDirectory =
			Path.GetFullPath(Path.Combine(loaderDirectory, relativeDirectory));
		if (!IsUnderDirectory(variantDirectory, fullLibRoot)
			|| !string.Equals(
				Path.GetFileName(variantDirectory),
				compatTarget,
				StringComparison.Ordinal))
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Ignoring invalid variant directory " +
				$"'{relativeDirectory}'.");
			return null;
		}

		string markerPath = Path.Combine(variantDirectory, CompatTargetMarkerName);
		if (!File.Exists(markerPath)
			|| !string.Equals(
				File.ReadAllText(markerPath).Trim(),
				compatTarget,
				StringComparison.Ordinal))
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Ignoring variant with missing or " +
				$"mismatched marker: {markerPath}");
			return null;
		}

		string assemblyName = string.IsNullOrWhiteSpace(entry.Assembly)
			? RealDllName
			: entry.Assembly.Trim();
		if (!string.Equals(assemblyName, RealDllName, StringComparison.Ordinal))
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Ignoring unexpected assembly " +
				$"'{assemblyName}'.");
			return null;
		}

		string dllPath = Path.Combine(variantDirectory, assemblyName);
		if (!File.Exists(dllPath) || !MatchesExpectedHash(dllPath, entry.Sha256))
		{
			Log.Error(
				$"[UniversalDominionSword.Loader] Ignoring missing or hash-mismatched " +
				$"variant: {dllPath}");
			return null;
		}

		return new VariantCandidate(compatTarget, version, dllPath);
	}

	private static bool IsUnderDirectory(string path, string root)
	{
		string normalizedRoot =
			root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;
		string normalizedPath =
			path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;
		return normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
	}

	private static bool MatchesExpectedHash(string path, string? expected)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return false;
		}

		using FileStream stream = File.OpenRead(path);
		string actual = Convert.ToHexString(SHA256.HashData(stream));
		return string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static HostVersionSnapshot ResolveHostVersion()
	{
		string? fallbackLabel = null;
		try
		{
			string? label = ReleaseInfoManager.Instance.ReleaseInfo?.Version;
			if (TryCaptureVersion(label, ref fallbackLabel, out HostVersionSnapshot snapshot))
			{
				return snapshot;
			}
		}
		catch
		{
		}

		foreach (string path in GetPublishedReleaseInfoPaths())
		{
			if (TryReadJsonVersion(
				path,
				ref fallbackLabel,
				out HostVersionSnapshot snapshot))
			{
				return snapshot;
			}
		}

		Version? assemblyVersion = typeof(SerializableRun).Assembly.GetName().Version;
		if (assemblyVersion != null
			&& (assemblyVersion.Major != 0
				|| assemblyVersion.Minor != 0
				|| assemblyVersion.Build != 0
				|| assemblyVersion.Revision != 0))
		{
			return new HostVersionSnapshot(assemblyVersion, fallbackLabel);
		}

		return new HostVersionSnapshot(null, fallbackLabel);
	}

	private static IEnumerable<string> GetPublishedReleaseInfoPaths()
	{
		string? executablePath = TryCallGodotOsString("GetExecutablePath");
		string? executableDirectory = string.IsNullOrWhiteSpace(executablePath)
			? null
			: Path.GetDirectoryName(executablePath);
		if (string.IsNullOrWhiteSpace(executableDirectory))
		{
			yield break;
		}

		if (string.Equals(
			TryCallGodotOsString("GetName"),
			"macOS",
			StringComparison.Ordinal))
		{
			yield return Path.Combine(
				executableDirectory,
				"..",
				"Resources",
				"release_info.json");
		}
		yield return Path.Combine(executableDirectory, "release_info.json");
	}

	private static string? TryCallGodotOsString(string methodName)
	{
		try
		{
			Type? osType =
				Type.GetType("Godot.OS, GodotSharp", throwOnError: false)
				?? Type.GetType("Godot.OS, GodotSharpEditor", throwOnError: false)
				?? AppDomain.CurrentDomain.GetAssemblies()
					.Select(assembly => assembly.GetType("Godot.OS", throwOnError: false))
					.FirstOrDefault(type => type != null);
			MethodInfo? method = osType?.GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			return method?.Invoke(null, null) as string;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryReadJsonVersion(
		string path,
		ref string? fallbackLabel,
		out HostVersionSnapshot snapshot)
	{
		snapshot = default;
		try
		{
			if (!File.Exists(path))
			{
				return false;
			}

			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
			return document.RootElement.TryGetProperty("version", out JsonElement version)
				&& TryCaptureVersion(
					version.GetString(),
					ref fallbackLabel,
					out snapshot);
		}
		catch
		{
			return false;
		}
	}

	private static bool TryCaptureVersion(
		string? label,
		ref string? fallbackLabel,
		out HostVersionSnapshot snapshot)
	{
		snapshot = default;
		if (string.IsNullOrWhiteSpace(label))
		{
			return false;
		}

		fallbackLabel ??= label;
		if (!TryParseVersion(label, out Version version))
		{
			return false;
		}

		snapshot = new HostVersionSnapshot(version, label);
		return true;
	}

	private static bool TryParseVersion(string text, out Version version)
	{
		string value = text.Trim();
		int suffixIndex = value.IndexOfAny(['-', '+']);
		if (suffixIndex >= 0)
		{
			value = value[..suffixIndex].Trim();
		}
		if (value.Length >= 2
			&& (value[0] == 'v' || value[0] == 'V')
			&& char.IsDigit(value[1]))
		{
			value = value[1..];
		}

		if (Version.TryParse(value, out Version? parsed))
		{
			version = parsed;
			return true;
		}

		version = new Version(0, 0);
		return false;
	}

	private sealed record VariantCandidate(
		string CompatTarget,
		Version Version,
		string DllPath);

	private readonly record struct HostVersionSnapshot(
		Version? Numeric,
		string? ReleaseLabel);

	private sealed class BundleVariantManifest
	{
		public List<BundleVariantEntry>? Variants { get; set; }
	}

	private sealed class BundleVariantEntry
	{
		public string? CompatTarget { get; set; }
		public string? Directory { get; set; }
		public string? Assembly { get; set; }
		public string? Sha256 { get; set; }
	}
}
