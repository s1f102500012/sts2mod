using System.Reflection;
using System.Text;
using HarmonyLib;
using HextechRunes;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static string PatchManifestFileName => $"patch_manifest.{ModInfo.TargetGameVersion}.txt";

	private const BindingFlags PatchMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	/// <summary>
	/// 补丁清单快照:每个补丁类的 id、目标(声明类型/方法名/参数/成员种类)、补丁方法种类、优先级与 before/after 约束。
	/// 目标集合、优先级或执行序约束变化必须同步更新清单(HEXTECH_WRITE_PATCH_MANIFEST=1 重生成),
	/// 这样"多打了一个原版方法"或"执行序被改"都会在测试里显形,而不是在玩家的联机里。
	/// 比对按整份序列(含重复行与顺序),不用集合差。
	/// </summary>
	private static void PatchManifestMatchesCheckedInList()
	{
		string manifestPath = Path.Combine(AppContext.BaseDirectory, PatchManifestFileName);
		string[] actual = BuildPatchManifest();
		if (Environment.GetEnvironmentVariable("HEXTECH_WRITE_PATCH_MANIFEST") == "1")
		{
			string sourcePath = Path.Combine(FindTestsSourceDirectory(), PatchManifestFileName);
			File.WriteAllText(sourcePath, "# 由 HEXTECH_WRITE_PATCH_MANIFEST=1 生成;每行 = 补丁类 id | 目标 | 补丁方法(优先级; before/after)。\n" + string.Join("\n", actual) + "\n", Encoding.UTF8);
			Console.WriteLine($"patch manifest written: {sourcePath}");
		}

		Expect(File.Exists(manifestPath), $"{PatchManifestFileName} should exist at {manifestPath} (run with HEXTECH_WRITE_PATCH_MANIFEST=1 to generate)");
		string[] expected = File.ReadAllLines(manifestPath)
			.Select(static line => line.Trim())
			.Where(static line => line.Length > 0 && !line.StartsWith('#'))
			.ToArray();

		if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
		{
			string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
			string[] unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
			int firstDifference = Enumerable.Range(0, Math.Max(expected.Length, actual.Length))
				.First(index => index >= expected.Length || index >= actual.Length || !string.Equals(expected[index], actual[index], StringComparison.Ordinal));
			Expect(
				false,
				$"patch manifest drift at line {firstDifference + 1} (expected {expected.Length} lines, built {actual.Length}); missing from build: [{string.Join("; ", missing)}] new in build: [{string.Join("; ", unexpected)}]");
		}
	}

	/// <summary>
	/// 补丁声明完整性:每个 <c>[HextechPatch]</c> 类型必须有可解析的 Harmony 目标或 <c>Apply(Harmony)</c>;
	/// 补丁 id 全局唯一;属性式补丁至少声明一个 prefix/postfix/finalizer/transpiler。
	/// 这条护栏防的是"属性挂错类导致补丁凭空消失,而清单快照照样通过"。
	/// </summary>
	private static void PatchDeclarationsResolveToRealTargets()
	{
		List<string> problems = [];
		Dictionary<string, Type> idOwners = new(StringComparer.Ordinal);
		foreach (Type type in typeof(ModEntry).Assembly.GetTypes())
		{
			HextechPatchAttribute? meta = type.GetCustomAttribute<HextechPatchAttribute>();
			List<HarmonyMethod> harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
			MethodInfo? dynamicApply = type.GetMethod("Apply", PatchMemberFlags, [typeof(Harmony)]);
			if (meta == null && harmonyAttributes.Count == 0)
			{
				continue;
			}

			string name = type.FullName ?? type.Name;
			if (Environment.GetEnvironmentVariable("HEXTECH_PATCH_DECL_TRACE") == "1")
			{
				Console.WriteLine($"  checking {name}");
			}

			if (meta == null)
			{
				problems.Add($"{name}: [HarmonyPatch] without [HextechPatch] metadata");
				continue;
			}

			if (idOwners.TryGetValue(meta.Id, out Type? owner))
			{
				problems.Add($"{name}: duplicate patch id '{meta.Id}' (also on {owner.FullName})");
			}
			else
			{
				idOwners[meta.Id] = type;
			}

			if (harmonyAttributes.Count == 0)
			{
				if (dynamicApply == null)
				{
					problems.Add($"{name} ({meta.Id}): no [HarmonyPatch] target and no Apply(Harmony)");
				}

				continue;
			}

			MethodInfo[] methods = type.GetMethods(PatchMemberFlags);
			bool hasPatchMethod = methods.Any(static method =>
				method.GetCustomAttribute<HarmonyPrefix>() != null
				|| method.GetCustomAttribute<HarmonyPostfix>() != null
				|| method.GetCustomAttribute<HarmonyFinalizer>() != null
				|| method.GetCustomAttribute<HarmonyTranspiler>() != null);
			if (!hasPatchMethod)
			{
				problems.Add($"{name} ({meta.Id}): declares a target but no patch method");
			}

			// 不在测试进程里执行 [HarmonyPrepare] / [HarmonyTargetMethod(s)]:它们可能触碰 Godot 运行时(测试进程没有原生层,
			// 触碰即段错误)。带这两种方法的类只校验声明形状,目标是否真的打上由 HextechPatcher 在 headless 加载时兜底
			// (打不上任何方法就记失败并告警)。
			string? failure = DescribeUnresolvedTarget(HarmonyMethod.Merge(harmonyAttributes), type, methods);
			if (failure != null && !meta.Optional)
			{
				problems.Add($"{name} ({meta.Id}): {failure}");
			}
		}

		Expect(problems.Count == 0, "patch declaration problems:\n  " + string.Join("\n  ", problems));
	}

	private static string? DescribeUnresolvedTarget(HarmonyMethod merged, Type patchType, MethodInfo[] methods)
	{
		MethodInfo? targetMethod = methods.FirstOrDefault(static method => method.GetCustomAttribute<HarmonyTargetMethod>() != null);
		MethodInfo? targetMethods = methods.FirstOrDefault(static method => method.GetCustomAttribute<HarmonyTargetMethods>() != null);
		if (targetMethod != null || targetMethods != null)
		{
			return null; // 运行时枚举目标,由 HextechPatcher 在加载时校验
		}

		if (merged.declaringType == null)
		{
			return "no declaring type";
		}

		Type declaringType = merged.declaringType;
		MethodType methodType = merged.methodType ?? MethodType.Normal;
		MethodBase? resolved = methodType switch
		{
			MethodType.Constructor => AccessTools.Constructor(declaringType, merged.argumentTypes),
			MethodType.StaticConstructor => declaringType.TypeInitializer,
			MethodType.Getter => merged.methodName == null ? null : AccessTools.PropertyGetter(declaringType, merged.methodName),
			MethodType.Setter => merged.methodName == null ? null : AccessTools.PropertySetter(declaringType, merged.methodName),
			_ => merged.methodName == null ? null : AccessTools.Method(declaringType, merged.methodName, merged.argumentTypes)
		};
		return resolved == null ? $"target {DescribePatchTarget(merged, patchType)} does not resolve" : null;
	}

	private static string FindTestsSourceDirectory()
	{
		string? directory = AppContext.BaseDirectory;
		while (directory != null && !File.Exists(Path.Combine(directory, "HextechRunes.Tests.csproj")))
		{
			directory = Path.GetDirectoryName(directory);
		}

		return directory ?? throw new InvalidOperationException("tests source directory not found");
	}

	internal static string[] BuildPatchManifest()
	{
		List<string> lines = [];
		foreach (Type type in typeof(ModEntry).Assembly.GetTypes())
		{
			HextechPatchAttribute? meta = type.GetCustomAttribute<HextechPatchAttribute>();
			List<HarmonyMethod> harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
			bool isDynamic = meta != null && harmonyAttributes.Count == 0
				&& type.GetMethod("Apply", PatchMemberFlags, [typeof(Harmony)]) != null;
			if (harmonyAttributes.Count == 0 && !isDynamic)
			{
				continue;
			}

			string id = meta?.Id ?? type.FullName ?? type.Name;
			if (isDynamic)
			{
				lines.Add($"{id} | dynamic({type.Name}.Apply)");
				continue;
			}

			HarmonyMethod merged = HarmonyMethod.Merge(harmonyAttributes);
			string target = DescribePatchTarget(merged, type);
			List<string> kinds = [];
			foreach (MethodInfo method in type.GetMethods(PatchMemberFlags))
			{
				string? kind = method.GetCustomAttribute<HarmonyPrefix>() != null ? "prefix"
					: method.GetCustomAttribute<HarmonyPostfix>() != null ? "postfix"
					: method.GetCustomAttribute<HarmonyFinalizer>() != null ? "finalizer"
					: method.GetCustomAttribute<HarmonyTranspiler>() != null ? "transpiler"
					: null;
				if (kind == null)
				{
					continue;
				}

				int priority = method.GetCustomAttribute<HarmonyPriority>()?.info.priority ?? (merged.priority >= 0 ? merged.priority : Priority.Normal);
				string skip = kind == "prefix" && method.ReturnType == typeof(bool) ? "?" : string.Empty;
				string ordering = DescribeOrdering(
					method.GetCustomAttribute<HarmonyBefore>()?.info.before ?? merged.before,
					method.GetCustomAttribute<HarmonyAfter>()?.info.after ?? merged.after);
				kinds.Add($"{kind}{skip}({priority}{ordering})");
			}

			kinds.Sort(StringComparer.Ordinal);
			lines.Add($"{id} | {target} | {string.Join(" ", kinds)}");
		}

		lines.Sort(StringComparer.Ordinal);
		return lines.ToArray();
	}

	private static string DescribeOrdering(string[]? before, string[]? after)
	{
		StringBuilder builder = new();
		if (before is { Length: > 0 })
		{
			builder.Append("; before:").Append(string.Join(",", before));
		}

		if (after is { Length: > 0 })
		{
			builder.Append("; after:").Append(string.Join(",", after));
		}

		return builder.ToString();
	}

	private static string DescribePatchTarget(HarmonyMethod merged, Type patchType)
	{
		if (merged.declaringType == null && merged.methodName == null)
		{
			bool hasTargetMethod = patchType.GetMethods(PatchMemberFlags)
				.Any(static method => method.GetCustomAttribute<HarmonyTargetMethod>() != null || method.GetCustomAttribute<HarmonyTargetMethods>() != null);
			return hasTargetMethod ? $"targetmethod({patchType.Name})" : "<unresolved>";
		}

		string arguments = merged.argumentTypes == null
			? "*"
			: string.Join(",", merged.argumentTypes.Select(static argument => argument.Name));
		string memberKind = merged.methodType is null or MethodType.Normal ? string.Empty : $" [{merged.methodType}]";
		return $"{merged.declaringType?.FullName}.{merged.methodName}({arguments}){memberKind}";
	}
}
