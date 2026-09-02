using System.Security.Cryptography;
using System.Text;
using HarmonyLib;

namespace HextechRunes;

/// <summary>
/// 原版拷贝守卫:凡本模组用 <c>bool</c> 前缀可能跳过原方法的目标,都可能复制了一段原版逻辑。
/// 游戏更新后这些拷贝会静默失真,因此启动时比对目标方法 IL 的 SHA1 与冻结表,漂移即告警。
/// </summary>
/// <remarks>
/// 冻结表由 <c>HEXTECH_DUMP_PATCHES</c> 的补丁表导出生成(每个目标附 <c>il=</c> 列),
/// 以嵌入资源 <c>vanilla_copy_guard.txt</c> 随各变体打包;没有表的变体跳过校验。
/// 行格式:<c>Namespace.Type::Method(ParamType,...)=sha1</c>。
/// </remarks>
internal static class HextechVanillaCopyGuard
{
	private const string ResourceName = "vanilla_copy_guard.txt";

	internal static string DescribeTarget(MethodBase method)
	{
		string parameters = string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name));
		return $"{method.DeclaringType?.FullName}::{method.Name}({parameters})";
	}

	internal static string? ComputeIlHash(MethodBase method)
	{
		byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
		return il == null ? null : Convert.ToHexString(SHA1.HashData(il)).ToLowerInvariant();
	}

	/// <summary>本模组挂了可跳过原方法的前缀的所有目标。</summary>
	internal static IEnumerable<MethodBase> EnumerateSkipCapableTargets(string ownerId)
	{
		foreach (MethodBase method in Harmony.GetAllPatchedMethods())
		{
			Patches? info = Harmony.GetPatchInfo(method);
			if (info != null && info.Prefixes.Any(patch => patch.owner == ownerId && patch.PatchMethod.ReturnType == typeof(bool)))
			{
				yield return method;
			}
		}
	}

	internal static IReadOnlyDictionary<string, string> LoadExpectedHashes()
	{
		Dictionary<string, string> expected = new(StringComparer.Ordinal);
		using Stream? stream = typeof(HextechVanillaCopyGuard).Assembly.GetManifestResourceStream(ResourceName);
		if (stream == null)
		{
			return expected;
		}

		using StreamReader reader = new(stream, Encoding.UTF8);
		while (reader.ReadLine() is string line)
		{
			string trimmed = line.Trim();
			int separator = trimmed.LastIndexOf('=');
			if (trimmed.Length == 0 || trimmed.StartsWith('#') || separator <= 0)
			{
				continue;
			}

			expected[trimmed[..separator]] = trimmed[(separator + 1)..];
		}

		return expected;
	}

	internal static void Verify(string ownerId)
	{
		try
		{
			IReadOnlyDictionary<string, string> expected = LoadExpectedHashes();
			if (expected.Count == 0)
			{
				HextechLog.Info($"[{ModInfo.Id}][VanillaCopyGuard] No frozen IL table for compat target {ModInfo.TargetGameVersion}; skipping.");
				return;
			}

			List<string> drifted = [];
			List<string> unregistered = [];
			foreach (MethodBase method in EnumerateSkipCapableTargets(ownerId))
			{
				string key = DescribeTarget(method);
				string? actual = ComputeIlHash(method);
				if (!expected.TryGetValue(key, out string? frozen))
				{
					unregistered.Add(key);
				}
				else if (!string.Equals(frozen, actual, StringComparison.Ordinal))
				{
					drifted.Add($"{key} frozen={frozen} actual={actual ?? "<no body>"}");
				}
			}

			if (drifted.Count > 0)
			{
				Log.Warn($"[{ModInfo.Id}][VanillaCopyGuard] DRIFT: {drifted.Count} patched method(s) changed IL since the table was frozen; review the prefixes that replace vanilla logic:\n  {string.Join("\n  ", drifted)}");
			}

			if (unregistered.Count > 0)
			{
				HextechLog.Info($"[{ModInfo.Id}][VanillaCopyGuard] {unregistered.Count} skip-capable target(s) not in the frozen table:\n  {string.Join("\n  ", unregistered)}");
			}

			if (drifted.Count == 0)
			{
				HextechLog.Info($"[{ModInfo.Id}][VanillaCopyGuard] {expected.Count} frozen target(s) verified.");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][VanillaCopyGuard] Verification failed: {ex.GetType().Name}: {ex.Message}");
		}
	}
}
