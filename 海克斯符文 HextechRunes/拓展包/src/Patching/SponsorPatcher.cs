using System.Reflection;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace HextechRunesSponsorPack;

/// <summary>
/// 属性式补丁的统一应用入口:本体 <c>HextechPatcher</c> 的最小同构副本(本体那份是 internal,拓展包无法复用)。
/// 逐类应用、逐条汇报,失败按功能归因;同一目标上的执行序只由 <c>[HarmonyPriority]</c> 决定,不依赖类的声明顺序。
/// </summary>
internal static class SponsorPatcher
{
	private const string DumpEnvVar = "HEXTECH_SPONSOR_DUMP_PATCHES";

	private sealed record PatchResult(string Id, string Feature, bool Applied, string? Error);

	private static readonly List<PatchResult> Results = [];

	/// <summary>
	/// 应用 <paramref name="assembly"/> 里的补丁类:带 <c>[HarmonyPatch]</c> 的走 Harmony 类处理器;
	/// 只带 <c>[SponsorPatch]</c> 且声明 <c>static void Apply(Harmony)</c> 的是"动态目标"补丁(目标只能在运行时枚举)。
	/// </summary>
	internal static void ApplyAll(Harmony harmony, Assembly assembly)
	{
		foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
		{
			SponsorPatchAttribute? meta = type.GetCustomAttribute<SponsorPatchAttribute>();
			bool hasHarmonyAttributes = HarmonyMethodExtensions.GetFromType(type).Any();
			MethodInfo? dynamicApply = hasHarmonyAttributes || meta == null
				? null
				: type.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(Harmony)]);
			if (!hasHarmonyAttributes && dynamicApply == null)
			{
				if (meta != null)
				{
					// 声明了元数据却没有任何目标:属性挂错了类。静默跳过等于补丁凭空消失,必须显形。
					Results.Add(new PatchResult(meta.Id, meta.Feature, Applied: false, Error: "no [HarmonyPatch] target and no Apply(Harmony)"));
					Log.Warn($"[{ModInfo.Id}][Patch] Patch declared but has no target: {meta.Id} ({meta.Feature}) on {type.FullName}", 2);
				}

				continue;
			}

			string id = meta?.Id ?? type.FullName ?? type.Name;
			string feature = meta?.Feature ?? "unspecified";
			try
			{
				if (dynamicApply != null)
				{
					dynamicApply.Invoke(null, [harmony]);
				}
				else
				{
					List<MethodInfo>? patched = harmony.CreateClassProcessor(type).Patch();
					if ((patched == null || patched.Count == 0) && meta?.Optional != true)
					{
						throw new InvalidOperationException("class processor patched no methods");
					}
				}

				Results.Add(new PatchResult(id, feature, Applied: true, Error: null));
			}
			catch (Exception ex)
			{
				Exception root = ex switch
				{
					HarmonyException { InnerException: not null } harmonyException => harmonyException.InnerException!,
					TargetInvocationException { InnerException: not null } invocation => invocation.InnerException!,
					_ => ex
				};
				Results.Add(new PatchResult(id, feature, Applied: false, Error: $"{root.GetType().Name}: {root.Message}"));
				string message = $"[{ModInfo.Id}][Patch] {(meta?.Optional == true ? "Optional patch skipped" : "Patch failed")}: {id} ({feature}): {root.GetType().Name}: {root.Message}";
				if (meta?.Optional == true)
				{
					Log.Info(message);
				}
				else
				{
					Log.Warn(message, 2);
				}
			}
		}
	}

	/// <summary>启动汇总:应用/失败计数,失败项逐条列出。</summary>
	internal static void LogSummary()
	{
		int failed = Results.Count(result => !result.Applied);
		Log.Info($"[{ModInfo.Id}][Patch] Applied {Results.Count - failed}/{Results.Count} patch classes.");
		foreach (PatchResult result in Results.Where(result => !result.Applied))
		{
			Log.Info($"[{ModInfo.Id}][Patch]   failed {result.Id} ({result.Feature}): {result.Error}");
		}
	}

	/// <summary>
	/// 环境变量 <c>HEXTECH_SPONSOR_DUMP_PATCHES=&lt;path&gt;</c> 存在时把本模组的补丁表写成文本:
	/// 重构等价性验证工具,目标集合、种类、优先级与同目标执行序在改动前后必须一致。
	/// </summary>
	internal static void DumpIfRequested(Harmony harmony)
	{
		string? path = Environment.GetEnvironmentVariable(DumpEnvVar);
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		try
		{
			File.WriteAllText(path, SponsorPatchTable.Build(harmony.Id), Encoding.UTF8);
			Log.Info($"[{ModInfo.Id}][Patch] Patch table written to {path}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Patch] Patch table dump failed: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}
}
