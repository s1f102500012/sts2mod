using System.Collections;
using System.Security.Cryptography;
using System.Text;
using MegaCrit.Sts2.Core.Modding;

namespace HextechRunes;

/// <summary>
/// 联机兼容性诊断签名。只用于日志与排障,不参与任何原版校验:
/// 模组清单比对(ModMismatch)完全由原版按 manifest 的 id/version 完成,wire 协议变化时抬 manifest 版本即可。
/// </summary>
internal static class HextechMultiplayerDiagnostics
{
	private const string SponsorPackModId = "HextechRunesSponsorPack";
	private static readonly string[] DiagnosedModIds = [ ModInfo.Id, SponsorPackModId ];

	private static string? _cachedNetworkSignature;

	internal static string GetNetworkSignature()
	{
		if (_cachedNetworkSignature != null)
		{
			return _cachedNetworkSignature;
		}

		List<string> signatures = [];
		foreach (string modId in DiagnosedModIds)
		{
			if (TryGetLoadedMod(modId, out Mod? mod) && mod != null)
			{
				signatures.Add(BuildModNetworkSignature(mod, includeSavedProperties: string.Equals(modId, ModInfo.Id, StringComparison.Ordinal)));
			}
		}

		if (signatures.Count == 0)
		{
			string? dllPath = Assembly.GetExecutingAssembly().Location;
			string? modDir = string.IsNullOrWhiteSpace(dllPath) ? null : Path.GetDirectoryName(dllPath);
			string pckPath = modDir == null ? string.Empty : Path.Combine(modDir, $"{ModInfo.Id}.pck");
			string manifestPath = modDir == null ? string.Empty : Path.Combine(modDir, $"{ModInfo.Id}.json");
			signatures.Add(BuildModNetworkSignature(ModInfo.Id, ModInfo.Version, dllPath, pckPath, manifestPath, includeSavedProperties: true));
		}

		_cachedNetworkSignature = string.Join("|", signatures);
		return _cachedNetworkSignature;
	}

	internal static void LogNetworkSignature()
	{
		HextechLog.Info($"[{ModInfo.Id}][MultiplayerCompat] Network compatibility signature: {GetNetworkSignature()}");
	}

	private static bool TryGetLoadedMod(string modId, out Mod? result)
	{
		foreach (Mod mod in ModManager.GetLoadedMods())
		{
			if (string.Equals(mod.manifest?.id, modId, StringComparison.Ordinal))
			{
				result = mod;
				return true;
			}
		}

		result = null;
		return false;
	}

	private static string BuildModNetworkSignature(Mod mod, bool includeSavedProperties)
	{
		string modId = mod.manifest?.id ?? "unknown";
		string version = mod.manifest?.version ?? "unknown";
		string dllPath = Path.Combine(mod.path, $"{modId}.dll");
		string pckPath = Path.Combine(mod.path, $"{modId}.pck");
		string manifestPath = Path.Combine(mod.path, $"{modId}.json");
		return BuildModNetworkSignature(modId, version, dllPath, pckPath, manifestPath, includeSavedProperties);
	}

	internal static string BuildModNetworkSignature(string modId, string version, string? dllPath, string pckPath, string manifestPath, bool includeSavedProperties)
	{
		string signature = $"id={modId};version={version};target={ModInfo.TargetGameVersion};dll={ShortFileHash(dllPath)};pck={ShortFileHash(pckPath)};manifest={ShortFileHash(manifestPath)}";
		return includeSavedProperties
			? $"{signature};savedProps={BuildSavedPropertiesSignature()}"
			: signature;
	}

	private static string BuildSavedPropertiesSignature()
	{
		const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
		List<string> propertyNames = [];
		object? rawMap = typeof(SavedPropertiesTypeCache)
			.GetField("_netIdToPropertyNameMap", flags)
			?.GetValue(null);

		if (rawMap is IEnumerable enumerable)
		{
			foreach (object? item in enumerable)
			{
				propertyNames.Add(item?.ToString() ?? string.Empty);
			}
		}

#if STS2_109_OR_NEWER
		int netIdBitSize = SavedPropertiesTypeCache.PropertyIdBitSize;
#else
		int netIdBitSize = SavedPropertiesTypeCache.NetIdBitSize;
#endif
		string payload = $"{netIdBitSize}\n{string.Join("\n", propertyNames)}";
		return $"{netIdBitSize}/{propertyNames.Count}/{ShortHash(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant())}";
	}

	private static string ShortFileHash(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "missing";
		}

		try
		{
			if (!File.Exists(path))
			{
				return "missing";
			}

			using FileStream stream = File.OpenRead(path);
			return ShortHash(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Failed to hash {Path.GetFileName(path)}: {ex.Message}");
			return "error";
		}
	}

	private static string ShortHash(string hash)
	{
		return hash.Length <= 16 ? hash : hash[..16];
	}
}
