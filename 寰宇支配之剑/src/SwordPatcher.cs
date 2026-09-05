using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace UniversalDominionSword;

/// <summary>
/// 属性式补丁的统一应用入口:逐类应用、逐条汇报,并在启动时列出与其他模组共享的补丁点。
/// 同一目标上的执行序只由 <c>[HarmonyPriority]</c> 决定,不依赖类的声明顺序。
/// </summary>
internal static class SwordPatcher
{
	private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private sealed record PatchResult(string Id, string Feature, Type PatchType, bool Applied, string? Error);

	private static readonly List<PatchResult> Results = [];

	internal static void ApplyAll(Harmony harmony, Assembly assembly)
	{
		foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
		{
			SwordPatchAttribute? meta = type.GetCustomAttribute<SwordPatchAttribute>();
			bool hasHarmonyAttributes = HarmonyMethodExtensions.GetFromType(type).Count > 0;
			if (!hasHarmonyAttributes)
			{
				if (meta != null)
				{
					// 声明了元数据却没有目标:属性挂错了类。静默跳过等于补丁凭空消失,必须显形。
					Results.Add(new PatchResult(meta.Id, meta.Feature, type, Applied: false, Error: "no [HarmonyPatch] target"));
					Log.Warn($"[{ModInfo.Id}][Patch] Patch declared but has no target: {meta.Id} ({meta.Feature}) on {type.FullName}");
				}

				continue;
			}

			string id = meta?.Id ?? type.FullName ?? type.Name;
			string feature = meta?.Feature ?? "unspecified";
			try
			{
				List<MethodInfo>? patched = harmony.CreateClassProcessor(type).Patch();
				bool gated = type.GetMethods(StaticMembers)
					.Any(static method => method.GetCustomAttribute<HarmonyPrepare>() != null);
				if ((patched == null || patched.Count == 0) && !gated && meta?.Optional != true)
				{
					throw new InvalidOperationException("class processor patched no methods");
				}

				bool skipped = patched == null || patched.Count == 0;
				Results.Add(new PatchResult(id, feature, type, Applied: !skipped, Error: skipped ? "skipped by [HarmonyPrepare]" : null));
			}
			catch (Exception exception)
			{
				Exception root = exception switch
				{
					HarmonyException { InnerException: not null } harmonyException => harmonyException.InnerException!,
					TargetInvocationException { InnerException: not null } invocation => invocation.InnerException!,
					_ => exception
				};
				Results.Add(new PatchResult(id, feature, type, Applied: false, Error: $"{root.GetType().Name}: {root.Message}"));
				if (meta?.Optional == true)
				{
					Log.Info($"[{ModInfo.Id}][Patch] Optional patch skipped: {id} ({feature}): {root.GetType().Name}: {root.Message}");
				}
				else
				{
					Log.Warn($"[{ModInfo.Id}][Patch] Patch failed: {id} ({feature}): {root.GetType().Name}: {root.Message}");
				}
			}
		}
	}

	internal static void LogSummary()
	{
		int failed = Results.Count(result => !result.Applied);
		Log.Info($"[{ModInfo.Id}][Patch] Applied {Results.Count - failed}/{Results.Count} patch classes.");
		foreach (PatchResult result in Results.Where(result => !result.Applied))
		{
			Log.Info($"[{ModInfo.Id}][Patch]   not applied {result.Id} ({result.Feature}): {result.Error}");
		}

		IReadOnlyList<string> missing = VanillaMembers.MissingMembers;
		if (missing.Count > 0)
		{
			Log.Warn($"[{ModInfo.Id}][Patch] {missing.Count} vanilla private member(s) missing in this game build (dependent features degraded):\n  {string.Join("\n  ", missing)}");
		}
	}

	/// <summary>列出本模组补丁与其他 owner 共享的目标方法。玩家报"装了别的模组后行为变样"时,这行日志就是答案。</summary>
	internal static void LogSharedPatchTargets(Harmony harmony)
	{
		try
		{
			List<string> lines = [];
			foreach (MethodBase method in Harmony.GetAllPatchedMethods())
			{
				HarmonyLib.Patches? info = Harmony.GetPatchInfo(method);
				if (info == null)
				{
					continue;
				}

				List<Patch> all = [.. info.Prefixes, .. info.Postfixes, .. info.Transpilers, .. info.Finalizers];
				if (!all.Any(patch => patch.owner == harmony.Id))
				{
					continue;
				}

				string[] others = all
					.Where(patch => patch.owner != harmony.Id)
					.Select(patch => patch.owner)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(owner => owner, StringComparer.Ordinal)
					.ToArray();
				if (others.Length > 0)
				{
					lines.Add($"{method.DeclaringType?.FullName}.{method.Name} <- {string.Join(", ", others)}");
				}
			}

			if (lines.Count == 0)
			{
				Log.Info($"[{ModInfo.Id}][Patch] No patch targets are shared with other mods.");
				return;
			}

			lines.Sort(StringComparer.Ordinal);
			Log.Info($"[{ModInfo.Id}][Patch] {lines.Count} patch target(s) shared with other mods:\n  {string.Join("\n  ", lines)}");
		}
		catch (Exception exception)
		{
			Log.Warn($"[{ModInfo.Id}][Patch] Could not enumerate shared patch targets: {exception.GetType().Name}: {exception.Message}");
		}
	}
}
