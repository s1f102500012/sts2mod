using System.Collections;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer;

namespace HextechRunes;

internal static class HextechMultiplayerCompatibilityHooks
{
	private const string SponsorPackModId = "HextechRunesSponsorPack";
	internal const string NetworkProtocolVersion = "net1";
	private static readonly string[] NetworkCheckedModIds = [ ModInfo.Id, SponsorPackModId ];

	private static bool _installed;
	private static string? _cachedNetworkSignature;

	public static void Install(Harmony harmony)
	{
		if (_installed)
		{
			return;
		}

		bool installedAny = false;
		MethodInfo? modListMethod = AccessTools.Method(typeof(ModManager), nameof(ModManager.GetGameplayRelevantModNameList));
		if (modListMethod != null)
		{
			installedAny |= TryPatch(
				harmony,
				modListMethod,
				"gameplay mod list protocol version",
				postfix: new HarmonyMethod(typeof(HextechMultiplayerCompatibilityHooks), nameof(GetGameplayRelevantModNameListPostfix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Could not patch gameplay mod list; multiplayer network protocol checks are unavailable.");
		}

		installedAny |= TryPatchPacketFinalizer(harmony, typeof(NetHostGameService), nameof(NetHostGameService.OnPacketReceived), nameof(NetHostGameServiceOnPacketReceivedFinalizer));
		installedAny |= TryPatchPacketFinalizer(harmony, typeof(NetClientGameService), nameof(NetClientGameService.OnPacketReceived), nameof(NetClientGameServiceOnPacketReceivedFinalizer));
		_installed = true;
		if (!installedAny)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] No multiplayer compatibility hooks were installed. The mod will continue to load, but multiplayer mismatch diagnostics may be unavailable on this platform.");
		}
	}

	private static void GetGameplayRelevantModNameListPostfix(ref List<string>? __result)
	{
		if (__result == null)
		{
			return;
		}

		foreach (string modId in NetworkCheckedModIds)
		{
			ReplaceGameplayRelevantEntry(__result, modId);
		}
	}

	private static void ReplaceGameplayRelevantEntry(List<string> entries, string modId)
	{
		int index = entries.FindIndex(entry => entry.StartsWith($"{modId}-", StringComparison.Ordinal));
		if (index < 0)
		{
			return;
		}

		if (!TryGetLoadedMod(modId, out Mod? mod) || mod?.manifest == null)
		{
			return;
		}

		entries[index] = BuildGameplayCompatibilityEntry(
			mod.manifest.id ?? modId,
			mod.manifest.version ?? "unknown");
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

	internal static string BuildGameplayCompatibilityEntry(string modId, string version)
	{
		// 短协议号随 wire schema、共享状态事务或战斗确定性规则变化而递增。
		// 不追加构建哈希，保证入房报错中的兼容条目可读且可人工核对。
		// 运行时仍由 NetHost/ClientGameService 的 SavedProperties 协议失配 finalizer 兜底断开。
		return $"{modId}-{version}-{NetworkProtocolVersion}";
	}

	private static Exception? NetHostGameServiceOnPacketReceivedFinalizer(Exception? __exception, NetHostGameService __instance, ulong senderId)
	{
		if (!IsSavedPropertiesProtocolException(__exception))
		{
			return __exception;
		}

		Log.Error($"[{ModInfo.Id}][MultiplayerCompat] Disconnecting peer {senderId} after SavedProperties protocol mismatch. This usually means players are using incompatible content under the same visible version and network protocol. localSignature={GetNetworkSignature()} exception={__exception}");
		try
		{
			__instance.DisconnectClient(senderId, NetError.ModMismatch, now: true);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Failed to disconnect incompatible peer {senderId}: {ex.Message}");
		}

		return null;
	}

	private static Exception? NetClientGameServiceOnPacketReceivedFinalizer(Exception? __exception, NetClientGameService __instance)
	{
		if (!IsSavedPropertiesProtocolException(__exception))
		{
			return __exception;
		}

		Log.Error($"[{ModInfo.Id}][MultiplayerCompat] Disconnecting from host after SavedProperties protocol mismatch. This usually means players are using incompatible content under the same visible version and network protocol. localSignature={GetNetworkSignature()} exception={__exception}");
		try
		{
			__instance.Disconnect(NetError.ModMismatch, now: true);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Failed to disconnect from incompatible host: {ex.Message}");
		}

		return null;
	}

	private static bool IsSavedPropertiesProtocolException(Exception? exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
		{
			string message = current.Message ?? string.Empty;
			// 栈帧类名用字面量:0.109 起缓存类真名是 ModelIdSerializationCache(mod 内经别名仍叫
			// SavedPropertiesTypeCache),两个名字都匹配以覆盖新旧版本的栈。
			if (message.Contains("SavedProperty net ID", StringComparison.Ordinal)
				|| current.StackTrace?.Contains("SavedPropertiesTypeCache", StringComparison.Ordinal) == true
				|| current.StackTrace?.Contains("ModelIdSerializationCache", StringComparison.Ordinal) == true
				|| current.StackTrace?.Contains(nameof(SavedProperties), StringComparison.Ordinal) == true)
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryPatchPacketFinalizer(Harmony harmony, Type type, string methodName, string finalizerName)
	{
		MethodInfo? target = AccessTools.Method(type, methodName);
		if (target == null)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Could not patch {type.Name}.{methodName}; protocol mismatch fail-safe is unavailable.");
			return false;
		}

		return TryPatch(
			harmony,
			target,
			$"{type.Name}.{methodName} protocol mismatch fail-safe",
			finalizer: new HarmonyMethod(typeof(HextechMultiplayerCompatibilityHooks), finalizerName));
	}

	private static bool TryPatch(Harmony harmony, MethodBase target, string label, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? finalizer = null)
	{
		try
		{
			harmony.Patch(target, prefix, postfix, finalizer: finalizer);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Skipped {label}: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	private static string GetNetworkSignature()
	{
		if (_cachedNetworkSignature != null)
		{
			return _cachedNetworkSignature;
		}

		List<string> signatures = [];
		foreach (string modId in NetworkCheckedModIds)
		{
			if (TryGetLoadedMod(modId, out Mod? mod) && mod != null)
			{
				signatures.Add(BuildDiagnosticModNetworkSignature(mod));
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
		HextechLog.Info($"[{ModInfo.Id}][MultiplayerCompat] Network compatibility signature: {_cachedNetworkSignature}");
		return _cachedNetworkSignature;
	}

	private static string BuildDiagnosticModNetworkSignature(Mod mod)
	{
		string modId = mod.manifest?.id ?? "unknown";
		bool includeSavedProperties = string.Equals(modId, ModInfo.Id, StringComparison.Ordinal);
		return BuildModNetworkSignature(mod, includeSavedProperties);
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
		string signature = $"id={modId};version={version};protocol={NetworkProtocolVersion};target={ModInfo.TargetGameVersion};dll={ShortFileHash(dllPath)};pck={ShortFileHash(pckPath)};manifest={ShortFileHash(manifestPath)}";
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

		if (rawMap is IList list)
		{
			foreach (object? item in list)
			{
				propertyNames.Add(item?.ToString() ?? string.Empty);
			}
		}
		else if (rawMap is IEnumerable enumerable)
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
		return $"{netIdBitSize}/{propertyNames.Count}/{ShortHash(ComputeSha256(Encoding.UTF8.GetBytes(payload)))}";
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
			return ShortHash(ComputeSha256(stream));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Failed to hash {Path.GetFileName(path)}: {ex.Message}");
			return "error";
		}
	}

	private static string ComputeSha256(Stream stream)
	{
		return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
	}

	private static string ComputeSha256(byte[] bytes)
	{
		return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	}

	private static string ShortHash(string hash)
	{
		return hash.Length <= 16 ? hash : hash[..16];
	}
}
