using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace IntegratedStrategyEvents;

internal static class IntegratedStrategyPatcher
{
	private static Harmony? _harmony;
	private static readonly HashSet<string> DisabledFeatures = new(StringComparer.Ordinal);
	private static readonly HashSet<string> ReportedConflicts = new(StringComparer.Ordinal);
	internal static bool IsAvailable(string feature) => IsDirectlyAvailable(feature)
		&& (feature != "temporary-map" || IsDirectlyAvailable("forced-events"));
	private static bool IsDirectlyAvailable(string feature) => !DisabledFeatures.Contains(feature) && IntegratedStrategyPrivateMembers.IsAvailable(feature);

	internal static void ApplyAll(Harmony harmony, Assembly assembly)
	{
		_harmony = harmony;
		int applied = 0, failed = 0, skipped = 0;
		HashSet<string> ids = new(StringComparer.Ordinal);
		List<(string Feature, MethodBase Target, Type Patch)> installed = [];
		foreach (Type type in IntegratedStrategyPatchCatalog.PatchTypes(assembly))
		{
			IntegratedStrategyPatchAttribute? meta = type.GetCustomAttribute<IntegratedStrategyPatchAttribute>();
			try
			{
				if (meta == null || !ids.Add(meta.Id)) throw new InvalidOperationException("Missing or duplicate patch metadata.");
				if (!IsAvailable(meta.Feature))
				{
					skipped++;
					Log.Info($"{ModInfo.LogPrefix}[Patch] Disabled {meta.Id}: {meta.Feature} is unavailable.");
					continue;
				}
				foreach ((Type targetType, MethodBase? target) in IntegratedStrategyPatchCatalog.Targets(type))
				{
					if (target == null && meta.Optional)
					{
						skipped++;
						Log.Info($"{ModInfo.LogPrefix}[Patch] Optional target absent: {meta.Id}, {targetType.FullName}.");
						continue;
					}
					try
					{
						if (target == null) throw new MissingMethodException(targetType.FullName);
						HarmonyMethod? prefix = IntegratedStrategyPatchCatalog.Descriptor(type, "Prefix");
						if (prefix?.method.ReturnType == typeof(bool)) IntegratedStrategyVanillaGuard.Verify(target);
						harmony.Patch(target, prefix, IntegratedStrategyPatchCatalog.Descriptor(type, "Postfix"),
							finalizer: IntegratedStrategyPatchCatalog.Descriptor(type, "Finalizer"));
						applied++;
						installed.Add((meta.Feature, target, type));
						Log.Info($"{ModInfo.LogPrefix}[Patch] Applied {meta.Id}: {IntegratedStrategyPatchCatalog.TargetKey(target)} ({meta.Scope}).");
					}
					catch (Exception ex)
					{
						failed++;
						DisabledFeatures.Add(meta.Feature);
						Log.Error($"{ModInfo.LogPrefix}[Patch] Failed {meta.Id} on {targetType.FullName}: {ex.GetBaseException().Message}");
					}
				}
			}
			catch (Exception ex)
			{
				failed++;
				if (meta != null) DisabledFeatures.Add(meta.Feature);
				Log.Error($"{ModInfo.LogPrefix}[Patch] Invalid declaration {type.FullName}: {ex.GetBaseException().Message}");
			}
		}
		// 同一功能依赖的替换必须完整；仅卸载本功能已经装上的具体方法，不触碰其他 owner。
		foreach (var patch in installed.Where(patch => !IsAvailable(patch.Feature)))
		{
			foreach (string kind in new[] { "Prefix", "Postfix", "Finalizer" })
				if (IntegratedStrategyPatchCatalog.PatchMethod(patch.Patch, kind) is MethodInfo method)
					harmony.Unpatch(patch.Target, method);
			applied--;
			Log.Warn($"{ModInfo.LogPrefix}[Patch] Rolled back {patch.Patch.Name}: {patch.Feature} is incomplete.");
		}
		Log.Info($"{ModInfo.LogPrefix}[Patch] Summary applied={applied}, failed={failed}, skipped={skipped}.");
		ReportConflicts();
		ModManager.OnModDetected += OnModDetected;
	}

	private static void OnModDetected(Mod _)
	{
		ReportConflicts();
		DumpIfRequested();
	}

	internal static void ReportConflicts()
	{
		if (_harmony == null) return;
		foreach (MethodBase target in Harmony.GetAllPatchedMethods())
		{
			Patches? info = Harmony.GetPatchInfo(target);
			if (info == null || !info.Owners.Contains(_harmony.Id)) continue;
			string[] others = info.Owners.Where(owner => owner != _harmony.Id).Order(StringComparer.Ordinal).ToArray();
			if (others.Length == 0) continue;
			string conflict = $"{IntegratedStrategyPatchCatalog.TargetKey(target)}: {string.Join(", ", others)}";
			if (ReportedConflicts.Add(conflict)) Log.Info($"{ModInfo.LogPrefix}[Conflict] {conflict}");
			List<MethodInfo> ordered = PatchProcessor.GetSortedPatchMethods(target, info.Prefixes.ToArray());
			int firstSkip = ordered.FindIndex(method => method.ReturnType == typeof(bool) && info.Prefixes.Any(p => p.owner == _harmony.Id && p.PatchMethod == method));
			if (firstSkip < 0) continue;
			foreach (Patch other in info.Prefixes.Where(p => p.owner != _harmony.Id && ordered.IndexOf(p.PatchMethod) > firstSkip))
			{
				string warning = $"{other.owner}.{other.PatchMethod.Name} runs after our skipping prefix on {IntegratedStrategyPatchCatalog.TargetKey(target)}; may be skipped.";
				if (ReportedConflicts.Add(warning)) Log.Warn($"{ModInfo.LogPrefix}[Conflict] {warning}");
			}
		}
	}

	internal static void DumpIfRequested()
	{
		string? path = Environment.GetEnvironmentVariable("ISE_DUMP_PATCHES");
		if (string.IsNullOrWhiteSpace(path) || _harmony == null) return;
		try
		{
			List<string> lines = [];
			foreach (MethodBase target in Harmony.GetAllPatchedMethods())
			{
				Patches? info = Harmony.GetPatchInfo(target);
				if (info == null || !info.Owners.Contains(_harmony.Id)) continue;
				foreach ((string kind, IEnumerable<Patch> patches) in new[] { ("Prefix", info.Prefixes.AsEnumerable()), ("Postfix", info.Postfixes.AsEnumerable()) })
				{
					List<MethodInfo> order = PatchProcessor.GetSortedPatchMethods(target, patches.ToArray());
					foreach (Patch patch in patches)
						lines.Add($"{IntegratedStrategyPatchCatalog.TargetKey(target)}|{kind}|{patch.owner}|{patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name}|priority={patch.priority}|order={order.IndexOf(patch.PatchMethod)}|before={string.Join(",", patch.before)}|after={string.Join(",", patch.after)}|il={IntegratedStrategyPatchCatalog.IlHash(target)}");
				}
			}
			File.WriteAllLines(path, lines.Order(StringComparer.Ordinal));
		}
		catch (Exception ex) { Log.Warn($"{ModInfo.LogPrefix}[Patch] Could not export patch table: {ex.Message}"); }
	}
}
