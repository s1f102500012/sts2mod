using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace IntegratedStrategyEvents.Loader;

[ModInitializer(nameof(Initialize))]
public static class LoaderBootstrap
{
	private const string ModId = "IntegratedStrategyEvents";
	private const string RealDllName = "IntegratedStrategyEvents.dll";
	private const string VariantManifestName = "integrated-strategy-events-variants.manifest";
	private const string CompatTargetMarkerName = "compat-target.txt";
	private const string CompatTargetMetadataKey = "IntegratedStrategyEventsCompatibilityTarget";

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
	private static bool _legacyAssociationCallbackInstalled;

	public static void Initialize()
	{
		string? loaderDirectory = Path.GetDirectoryName(typeof(LoaderBootstrap).Assembly.Location);
		if (string.IsNullOrWhiteSpace(loaderDirectory))
		{
			Log.Error("[IntegratedStrategyEvents.Loader] Could not resolve loader directory.");
			return;
		}

		try
		{
			string libRoot = Path.Combine(loaderDirectory, "lib");
			if (!Directory.Exists(libRoot))
			{
				throw new DirectoryNotFoundException($"Missing lib directory: {libRoot}");
			}

			HostVersionSnapshot host = ResolveHostVersion();
			VariantCandidate? variant = PickVariant(loaderDirectory, libRoot, host.Numeric);
			if (variant == null)
			{
				throw new InvalidOperationException(
					$"No compatible variant for host {host.ReleaseLabel ?? host.Numeric?.ToString() ?? "unknown"}.");
			}

			Log.Info(
				$"[IntegratedStrategyEvents.Loader] Host version label={host.ReleaseLabel ?? "<none>"} " +
				$"numeric={host.Numeric?.ToString() ?? "<none>"}; picked variant {variant.CompatTarget}.");

			AssemblyLoadContext context =
				AssemblyLoadContext.GetLoadContext(typeof(LoaderBootstrap).Assembly)
				?? AssemblyLoadContext.Default;
			Assembly implementation = context.LoadFromAssemblyPath(variant.DllPath);
			ValidateVariantAssembly(implementation, variant);
			if (AssociateVariantAssemblyWithGame(implementation))
				InvokeRealInitializer(implementation);
		}
		catch (Exception exception)
		{
			Log.Error($"[IntegratedStrategyEvents.Loader] Failed to load implementation: {exception}");
			throw;
		}
	}

	private static void ValidateVariantAssembly(Assembly assembly, VariantCandidate variant)
	{
		string? identity = assembly.GetName().Name;
		if (!string.Equals(identity, Path.GetFileNameWithoutExtension(RealDllName), StringComparison.Ordinal))
		{
			throw new BadImageFormatException(
				$"Variant identity is {identity ?? "<missing>"}, expected IntegratedStrategyEvents.");
		}

		string? embeddedTarget = assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(attribute =>
				string.Equals(attribute.Key, CompatTargetMetadataKey, StringComparison.Ordinal))
			?.Value;
		if (!string.Equals(embeddedTarget, variant.CompatTarget, StringComparison.Ordinal))
		{
			throw new BadImageFormatException(
				$"Variant metadata is {embeddedTarget ?? "<missing>"}, expected {variant.CompatTarget}.");
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
				$"[IntegratedStrategyEvents.Loader] Partial type load for {assembly.FullName}: " +
				$"{exception.Message}");
			return exception.Types.OfType<Type>();
		}
	}

	private static bool AssociateVariantAssemblyWithGame(Assembly assembly)
	{
		_selectedVariantAssembly = assembly;
		if (IsAssemblyAssociatedWithMod(assembly)) return true;
		if (AssociateAssemblyWithModMethod != null)
		{
			AssociateAssemblyWithModMethod.Invoke(null, [ModId, assembly]);
			if (IsAssemblyAssociatedWithMod(assembly))
			{
				Log.Info("[IntegratedStrategyEvents.Loader] Official assembly association; no reflection bridge.");
				return true;
			}
			throw new InvalidOperationException("Official assembly association did not register the implementation.");
		}
		// STS2 0.107.1 在 initializer 返回后覆盖 Mod.assembly；检测完成后才关联并初始化。
		if (LegacyModAssemblyField != null && !_legacyAssociationCallbackInstalled)
		{
			ModManager.OnModDetected += OnLegacyModDetected;
			_legacyAssociationCallbackInstalled = true;
			return false;
		}
		throw new MissingMemberException("No supported assembly association API; refusing partial type discovery.");
	}

	private static void OnLegacyModDetected(Mod mod)
	{
		if (_selectedVariantAssembly == null
			|| !string.Equals(ReadManifestId(mod), ModId, StringComparison.Ordinal))
		{
			return;
		}

		if (mod.state != ModLoadState.Loaded) return;
		LegacyModAssemblyField!.SetValue(mod, _selectedVariantAssembly);
		ModManager.OnModDetected -= OnLegacyModDetected;
		_legacyAssociationCallbackInstalled = false;
		Log.Info(
			$"[IntegratedStrategyEvents.Loader] Associated variant " +
			$"{_selectedVariantAssembly.GetName().Name} with the STS2 0.107.x mod record.");
		try { InvokeRealInitializer(_selectedVariantAssembly); }
		catch (Exception ex)
		{
			mod.state = ModLoadState.Failed;
			Log.Error($"[IntegratedStrategyEvents.Loader] Legacy initialization failed: {ex}");
		}
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
			ModInitializerAttribute? attribute = type.GetCustomAttribute<ModInitializerAttribute>();
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
			Log.Info(
				$"[IntegratedStrategyEvents.Loader] Invoked implementation initializer " +
				$"{type.FullName}.{initializer.Name}.");
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
		List<VariantCandidate> variants = LoadVariantManifest(loaderDirectory, libRoot)
			.OrderBy(candidate => candidate.Version)
			.ToList();
		if (host == null)
		{
			Log.Warn(
				"[IntegratedStrategyEvents.Loader] Host version is unknown; using newest bundled variant.");
			return variants.LastOrDefault();
		}

		return variants.LastOrDefault(candidate => candidate.Version <= host);
	}

	private static List<VariantCandidate> LoadVariantManifest(
		string loaderDirectory,
		string libRoot)
	{
		string manifestPath = Path.Combine(loaderDirectory, VariantManifestName);
		if (!File.Exists(manifestPath))
		{
			throw new FileNotFoundException("Missing variant manifest.", manifestPath);
		}

		BundleVariantManifest? manifest = JsonSerializer.Deserialize<BundleVariantManifest>(
			File.ReadAllText(manifestPath),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		if (manifest?.Variants == null || manifest.Variants.Count == 0)
		{
			throw new InvalidDataException($"Variant manifest contains no variants: {manifestPath}");
		}

		string fullLibRoot = Path.GetFullPath(libRoot);
		return manifest.Variants
			.Select(entry => CreateVariantCandidate(loaderDirectory, fullLibRoot, entry))
			.ToList();
	}

	private static VariantCandidate CreateVariantCandidate(
		string loaderDirectory,
		string fullLibRoot,
		BundleVariantEntry entry)
	{
		string compatTarget = entry.CompatTarget?.Trim() ?? string.Empty;
		if (!TryParseVersion(compatTarget, out Version version))
		{
			throw new InvalidDataException($"Invalid compatibility target '{entry.CompatTarget}'.");
		}

		string expectedDirectory = Path.Combine("lib", compatTarget);
		string relativeDirectory = entry.Directory?.Trim() ?? string.Empty;
		if (!string.Equals(
			relativeDirectory.Replace('\\', '/'),
			expectedDirectory.Replace('\\', '/'),
			StringComparison.Ordinal))
		{
			throw new InvalidDataException(
				$"Variant directory is '{relativeDirectory}', expected '{expectedDirectory}'.");
		}

		string variantDirectory = Path.GetFullPath(Path.Combine(loaderDirectory, relativeDirectory));
		if (!IsUnderDirectory(variantDirectory, fullLibRoot)
			|| !string.Equals(Path.GetFileName(variantDirectory), compatTarget, StringComparison.Ordinal))
		{
			throw new InvalidDataException($"Unsafe variant directory '{relativeDirectory}'.");
		}

		string markerPath = Path.Combine(variantDirectory, CompatTargetMarkerName);
		if (!File.Exists(markerPath)
			|| !string.Equals(File.ReadAllText(markerPath).Trim(), compatTarget, StringComparison.Ordinal))
		{
			throw new InvalidDataException($"Missing or mismatched marker: {markerPath}");
		}

		string assemblyName = entry.Assembly?.Trim() ?? string.Empty;
		if (!string.Equals(assemblyName, RealDllName, StringComparison.Ordinal))
		{
			throw new InvalidDataException($"Unexpected variant assembly '{assemblyName}'.");
		}

		string dllPath = Path.Combine(variantDirectory, assemblyName);
		if (!File.Exists(dllPath))
		{
			throw new FileNotFoundException("Missing variant assembly.", dllPath);
		}

		return new VariantCandidate(compatTarget, version, dllPath);
	}

	private static bool IsUnderDirectory(string path, string root)
	{
		string relative = Path.GetRelativePath(root, path);
		return !Path.IsPathRooted(relative)
			&& !string.Equals(relative, "..", StringComparison.Ordinal)
			&& !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
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
			if (TryReadJsonVersion(path, ref fallbackLabel, out HostVersionSnapshot snapshot))
			{
				return snapshot;
			}
		}

		Version? assemblyVersion = typeof(SerializableRun).Assembly.GetName().Version;
		if (assemblyVersion != null && assemblyVersion != new Version(0, 0, 0, 0))
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

		if (string.Equals(TryCallGodotOsString("GetName"), "macOS", StringComparison.Ordinal))
		{
			yield return Path.Combine(executableDirectory, "..", "Resources", "release_info.json");
		}
		yield return Path.Combine(executableDirectory, "release_info.json");
	}

	private static string? TryCallGodotOsString(string methodName)
	{
		try
		{
			Type? osType =
				Type.GetType("Godot.OS, GodotSharp", throwOnError: false)
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
				&& TryCaptureVersion(version.GetString(), ref fallbackLabel, out snapshot);
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

	private sealed record VariantCandidate(string CompatTarget, Version Version, string DllPath);
	private readonly record struct HostVersionSnapshot(Version? Numeric, string? ReleaseLabel);

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
