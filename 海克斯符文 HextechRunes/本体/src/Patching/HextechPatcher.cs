using System.Text;
using HarmonyLib;

namespace HextechRunes;

/// <summary>
/// 属性式补丁的统一应用入口:逐类应用、逐条汇报、失败按功能归因,并在启动时列出与其他模组共享的补丁点。
/// </summary>
/// <remarks>
/// 同一目标上本模组的多个补丁,执行序只由 <c>[HarmonyPriority]</c> 决定,不依赖类的声明顺序;
/// 需要先后关系的地方必须显式标优先级。
/// </remarks>
internal static class HextechPatcher
{
	private const string DumpEnvVar = "HEXTECH_DUMP_PATCHES";

	private sealed record PatchResult(string Id, string Feature, Type PatchType, bool Applied, string? Error);

	private static readonly List<PatchResult> Results = [];

	/// <summary>
	/// 应用 <paramref name="assembly"/> 中所有补丁类:带 <c>[HarmonyPatch]</c> 的走 Harmony 类处理器;
	/// 只带 <c>[HextechPatch]</c> 且声明 <c>static void Apply(Harmony)</c> 的是"动态目标"补丁
	/// (目标集合只能在运行时枚举,如所有已加载程序集里的 Orb 子类),由该方法自行逐个 Patch。
	/// </summary>
	internal static void ApplyAll(Harmony harmony, Assembly assembly)
	{
		foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
		{
			HextechPatchAttribute? meta = type.GetCustomAttribute<HextechPatchAttribute>();
			bool hasHarmonyAttributes = HarmonyMethodExtensions.GetFromType(type).Any();
			MethodInfo? dynamicApply = hasHarmonyAttributes || meta == null
				? null
				: type.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(Harmony)]);
			if (!hasHarmonyAttributes && dynamicApply == null)
			{
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
					harmony.CreateClassProcessor(type).Patch();
				}

				Results.Add(new PatchResult(id, feature, type, Applied: true, Error: null));
			}
			catch (Exception ex)
			{
				Exception root = ex switch
				{
					HarmonyException { InnerException: not null } harmonyException => harmonyException.InnerException!,
					TargetInvocationException { InnerException: not null } invocation => invocation.InnerException!,
					_ => ex
				};
				Results.Add(new PatchResult(id, feature, type, Applied: false, Error: $"{root.GetType().Name}: {root.Message}"));
				Type[] runes = meta?.AffectedRunes.ToArray() ?? [];
				if (runes.Length > 0)
				{
					foreach (Type rune in runes)
					{
						HextechRuntimeRuneCompatibility.MarkPlayerRuneHookFailed(rune, id, root);
					}
				}
				else if (meta?.Optional == true)
				{
					HextechLog.Info($"[{ModInfo.Id}][Patch] Optional patch skipped: {id} ({feature}): {root.GetType().Name}: {root.Message}");
				}
				else
				{
					Log.Warn($"[{ModInfo.Id}][Patch] Patch failed: {id} ({feature}): {root.GetType().Name}: {root.Message}");
				}
			}
		}
	}

	/// <summary>
	/// 只应用 <paramref name="outerType"/> 里声明的嵌套补丁类(测试用:隔离验证某一功能组的补丁)。
	/// 给了 <paramref name="nestedNames"/> 就只应用点名的那几个。
	/// </summary>
	internal static void ApplyNested(Harmony harmony, Type outerType, params string[] nestedNames)
	{
		foreach (Type nested in outerType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (nestedNames.Length > 0 && !nestedNames.Contains(nested.Name, StringComparer.Ordinal))
			{
				continue;
			}

			if (HarmonyMethodExtensions.GetFromType(nested).Any())
			{
				harmony.CreateClassProcessor(nested).Patch();
			}
		}
	}

	/// <summary>测试用:按外层类型 + 嵌套补丁类名定位补丁方法。</summary>
	internal static MethodInfo? FindPatchMethod(Type outerType, string nestedName, string methodName)
	{
		return outerType.GetNestedType(nestedName, BindingFlags.Public | BindingFlags.NonPublic)
			?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
	}

	/// <summary>启动汇总:应用/失败计数,失败项逐条列出。</summary>
	internal static void LogSummary()
	{
		int failed = Results.Count(result => !result.Applied);
		HextechLog.Info($"[{ModInfo.Id}][Patch] Applied {Results.Count - failed}/{Results.Count} patch classes.");
		foreach (PatchResult result in Results.Where(result => !result.Applied))
		{
			HextechLog.Info($"[{ModInfo.Id}][Patch]   failed {result.Id} ({result.Feature}): {result.Error}");
		}

		IReadOnlyList<string> missingMembers = HextechHookReflection.MissingMembers;
		if (missingMembers.Count > 0)
		{
			Log.Warn($"[{ModInfo.Id}][Patch] {missingMembers.Count} vanilla private member(s) missing in this game build (dependent features degraded):\n  {string.Join("\n  ", missingMembers)}");
		}
	}

	/// <summary>
	/// 列出本模组补丁与其他 owner 共享的目标方法。这是排查"装了别的模组后行为变样"的第一手线索。
	/// </summary>
	internal static void LogSharedPatchTargets(Harmony harmony)
	{
		try
		{
			List<string> lines = [];
			foreach (MethodBase method in Harmony.GetAllPatchedMethods())
			{
				Patches? info = Harmony.GetPatchInfo(method);
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
				HextechLog.Info($"[{ModInfo.Id}][Patch] No patch targets are shared with other mods.");
				return;
			}

			lines.Sort(StringComparer.Ordinal);
			Log.Info($"[{ModInfo.Id}][Patch] {lines.Count} patch target(s) shared with other mods:\n  {string.Join("\n  ", lines)}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Patch] Shared patch target scan failed: {ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>
	/// 环境变量 <c>HEXTECH_DUMP_PATCHES=&lt;path&gt;</c> 存在时,把本模组全部补丁按目标方法排序写成文本。
	/// 用于重构前后比对:目标集合、补丁种类、优先级与同目标内的执行序都必须一致。
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
			File.WriteAllText(path, BuildPatchTable(harmony.Id), Encoding.UTF8);
			Log.Info($"[{ModInfo.Id}][Patch] Patch table written to {path}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Patch] Patch table dump failed: {ex.GetType().Name}: {ex.Message}");
		}
	}

	internal static string BuildPatchTable(string ownerId)
	{
		StringBuilder builder = new();
		IEnumerable<MethodBase> methods = Harmony.GetAllPatchedMethods()
			.OrderBy(method => $"{method.DeclaringType?.FullName}.{method.Name}({string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})", StringComparer.Ordinal);
		foreach (MethodBase method in methods)
		{
			Patches? info = Harmony.GetPatchInfo(method);
			if (info == null)
			{
				continue;
			}

			List<string> lines = [];
			AppendKind(lines, "prefix", info.Prefixes, ownerId);
			AppendKind(lines, "postfix", info.Postfixes, ownerId);
			AppendKind(lines, "transpiler", info.Transpilers, ownerId);
			AppendKind(lines, "finalizer", info.Finalizers, ownerId);
			if (lines.Count == 0)
			{
				continue;
			}

			builder.Append(method.DeclaringType?.FullName).Append('.').Append(method.Name)
				.Append('(').Append(string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))).Append(')')
				.Append(" il=").Append(HextechVanillaCopyGuard.ComputeIlHash(method) ?? "<none>")
				.Append(" key=").Append(HextechVanillaCopyGuard.DescribeTarget(method))
				.Append('\n');
			foreach (string line in lines)
			{
				builder.Append("  ").Append(line).Append('\n');
			}
		}

		return builder.ToString();
	}

	private static void AppendKind(List<string> lines, string kind, IReadOnlyCollection<Patch> patches, string ownerId)
	{
		// Harmony 执行序:优先级降序,同优先级按加入序。before/after 只影响跨 owner 的相对序,这里按 owner 内视角记录。
		foreach (Patch patch in patches.Where(patch => patch.owner == ownerId).OrderByDescending(patch => patch.priority).ThenBy(patch => patch.index))
		{
			string extras = string.Empty;
			if (patch.before.Length > 0)
			{
				extras += $" before={string.Join("|", patch.before)}";
			}

			if (patch.after.Length > 0)
			{
				extras += $" after={string.Join("|", patch.after)}";
			}

			if (kind == "prefix" && patch.PatchMethod.ReturnType == typeof(bool))
			{
				extras += " skip=true";
			}

			lines.Add($"{kind} priority={patch.priority} {patch.PatchMethod.DeclaringType?.Name}.{patch.PatchMethod.Name}{extras}");
		}
	}
}
