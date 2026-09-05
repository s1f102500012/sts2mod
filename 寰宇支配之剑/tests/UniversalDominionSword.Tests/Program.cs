using System.Reflection;
using System.Text;
using HarmonyLib;

namespace UniversalDominionSword.Tests;

/// <summary>
/// 设计护栏(不需要 Godot 原生层):补丁清单快照、SavedProperty 属性名快照、补丁声明完整性、
/// 以及"本模组只有后缀"这条设计决定。测试进程里不执行 [HarmonyPrepare] / [HarmonyTargetMethod]。
/// </summary>
internal static class Program
{
	private const string WriteManifestsEnvVar = "UDS_WRITE_MANIFESTS";

	private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static string PatchManifestFileName => $"patch_manifest.{ModInfo.TargetGameVersion}.txt";

	private const string SavedPropertyManifestFileName = "saved_property_manifest.txt";

	private static int _passed;

	public static int Main()
	{
		try
		{
			Run(nameof(TargetGameVersionIsEmbedded), TargetGameVersionIsEmbedded);
			Run(nameof(PatchDeclarationsResolveToRealTargets), PatchDeclarationsResolveToRealTargets);
			Run(nameof(PatchesArePostfixOnly), PatchesArePostfixOnly);
			Run(nameof(PatchManifestMatchesCheckedInList), PatchManifestMatchesCheckedInList);
			Run(nameof(SavedPropertyManifestMatchesCheckedInList), SavedPropertyManifestMatchesCheckedInList);
			Run(nameof(NoForbiddenPatchTargets), NoForbiddenPatchTargets);
			Run(nameof(NoThirdPartyAssemblyReferences), NoThirdPartyAssemblyReferences);
			Console.WriteLine($"{_passed} checks passed against STS2 {ModInfo.TargetGameVersion}.");
			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception);
			return 1;
		}
	}

	private static void Run(string name, Action check)
	{
		check();
		_passed++;
		Console.WriteLine($"  ok {name}");
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}

	private static void TargetGameVersionIsEmbedded()
	{
		Expect(ModInfo.TargetGameVersion != "unknown", "assembly metadata UniversalDominionSwordCompatibilityTarget is missing");
		Expect(Version.TryParse(ModInfo.TargetGameVersion, out _), $"compat target '{ModInfo.TargetGameVersion}' is not a version");
	}

	private static IEnumerable<Type> PatchTypes()
	{
		return typeof(ModEntry).Assembly.GetTypes()
			.Where(type => type.GetCustomAttribute<SwordPatchAttribute>() != null
				|| HarmonyMethodExtensions.GetFromType(type).Count > 0)
			.OrderBy(type => type.FullName, StringComparer.Ordinal);
	}

	private static IEnumerable<MethodInfo> PatchMethods(Type type)
	{
		return type.GetMethods(StaticMembers).Where(static method =>
			method.GetCustomAttribute<HarmonyPrefix>() != null
			|| method.GetCustomAttribute<HarmonyPostfix>() != null
			|| method.GetCustomAttribute<HarmonyFinalizer>() != null
			|| method.GetCustomAttribute<HarmonyTranspiler>() != null);
	}

	/// <summary>每个补丁类都要有元数据、可解析目标与至少一个补丁方法;id 唯一。防"属性挂错类导致补丁凭空消失"。</summary>
	private static void PatchDeclarationsResolveToRealTargets()
	{
		List<string> problems = [];
		Dictionary<string, Type> idOwners = new(StringComparer.Ordinal);
		foreach (Type type in PatchTypes())
		{
			string name = type.FullName ?? type.Name;
			SwordPatchAttribute? meta = type.GetCustomAttribute<SwordPatchAttribute>();
			if (meta == null)
			{
				problems.Add($"{name}: [HarmonyPatch] without [SwordPatch] metadata");
				continue;
			}

			if (!idOwners.TryAdd(meta.Id, type))
			{
				problems.Add($"{name}: duplicate patch id '{meta.Id}' (also on {idOwners[meta.Id].FullName})");
			}

			List<HarmonyMethod> attributes = HarmonyMethodExtensions.GetFromType(type);
			if (attributes.Count == 0)
			{
				problems.Add($"{name} ({meta.Id}): no [HarmonyPatch] target");
				continue;
			}

			if (!PatchMethods(type).Any())
			{
				problems.Add($"{name} ({meta.Id}): declares a target but no patch method");
			}

			if (ResolveTarget(HarmonyMethod.Merge(attributes)) == null)
			{
				problems.Add($"{name} ({meta.Id}): target does not resolve: {DescribeTarget(HarmonyMethod.Merge(attributes))}");
			}
		}

		Expect(problems.Count == 0, "patch declaration problems:\n  " + string.Join("\n  ", problems));
	}

	/// <summary>设计决定:本模组不阻止任何原版方法,也不重写 IL。出现前缀/终结器/转译器就是设计变更,必须显形。</summary>
	private static void PatchesArePostfixOnly()
	{
		List<string> offenders = [];
		foreach (Type type in PatchTypes())
		{
			foreach (MethodInfo method in PatchMethods(type))
			{
				if (method.GetCustomAttribute<HarmonyPostfix>() == null)
				{
					offenders.Add($"{type.FullName}.{method.Name}");
				}
			}
		}

		Expect(offenders.Count == 0, "non-postfix patch methods found:\n  " + string.Join("\n  ", offenders));
	}

	/// <summary>绝不碰的中枢:Hook.* 分发、日志、联机通道、类型贡献入口、第三方类型。</summary>
	private static void NoForbiddenPatchTargets()
	{
		string[] forbiddenPrefixes =
		[
			"MegaCrit.Sts2.Core.Hooks.Hook",
			"MegaCrit.Sts2.Core.Logging.Log",
			"MegaCrit.Sts2.Core.Helpers.ReflectionHelper",
			"MegaCrit.Sts2.Core.Modding.ModManager",
			"MegaCrit.Sts2.Core.Multiplayer",
		];
		List<string> offenders = [];
		foreach (Type type in PatchTypes())
		{
			HarmonyMethod merged = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
			string declaring = merged.declaringType?.FullName ?? string.Empty;
			if (forbiddenPrefixes.Any(prefix => declaring.StartsWith(prefix, StringComparison.Ordinal))
				|| !declaring.StartsWith("MegaCrit.Sts2.", StringComparison.Ordinal))
			{
				offenders.Add($"{type.FullName} -> {declaring}");
			}
		}

		Expect(offenders.Count == 0, "patches on forbidden or third-party targets:\n  " + string.Join("\n  ", offenders));
	}

	/// <summary>主程序集只允许引用游戏本体、Godot、Harmony 与 .NET 运行时;任何第三方模组程序集都不得成为引用。</summary>
	private static void NoThirdPartyAssemblyReferences()
	{
		string[] allowed = ["sts2", "GodotSharp", "0Harmony", "System", "Microsoft", "netstandard", "mscorlib"];
		string[] offenders = typeof(ModEntry).Assembly.GetReferencedAssemblies()
			.Select(reference => reference.Name ?? string.Empty)
			.Where(name => !allowed.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();
		Expect(offenders.Length == 0, "unexpected assembly references: " + string.Join(", ", offenders));
	}

	/// <summary>补丁清单快照:补丁类 id | 目标 | 补丁方法(优先级; before/after)。目标集合或执行序变化必须同步更新清单。</summary>
	private static void PatchManifestMatchesCheckedInList()
	{
		string[] actual = BuildPatchManifest();
		MatchManifest(
			PatchManifestFileName,
			actual,
			"# 由 UDS_WRITE_MANIFESTS=1 生成;每行 = 补丁类 id | 目标 | 补丁方法(优先级; before/after)。");
	}

	/// <summary>SavedProperty 属性名快照:属性名集合决定联机 net-id 布局,增删改名都是不兼容变更。</summary>
	private static void SavedPropertyManifestMatchesCheckedInList()
	{
		string[] actual = typeof(ModEntry).Assembly.GetTypes()
			.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			.Where(property => property.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "SavedPropertyAttribute"))
			.Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
			.Distinct(StringComparer.Ordinal)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();
		MatchManifest(
			SavedPropertyManifestFileName,
			actual,
			"# 主程序集全部 [SavedProperty] 属性(类型.属性名,Ordinal 排序)。集合变化 = 联机 net-id 布局变化 = 与旧版本不兼容,必须升版本并写进更新日志。");
	}

	private static void MatchManifest(string fileName, string[] actual, string header)
	{
		string outputPath = Path.Combine(AppContext.BaseDirectory, fileName);
		if (Environment.GetEnvironmentVariable(WriteManifestsEnvVar) == "1")
		{
			string sourcePath = Path.Combine(FindTestsSourceDirectory(), fileName);
			File.WriteAllText(sourcePath, header + "\n" + string.Join("\n", actual) + "\n", new UTF8Encoding(false));
			File.Copy(sourcePath, outputPath, overwrite: true);
			Console.WriteLine($"    manifest written: {sourcePath}");
		}

		Expect(File.Exists(outputPath), $"{fileName} is missing (run with {WriteManifestsEnvVar}=1 to generate)");
		string[] expected = File.ReadAllLines(outputPath)
			.Select(static line => line.Trim())
			.Where(static line => line.Length > 0 && !line.StartsWith('#'))
			.ToArray();
		if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
		{
			string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
			string[] unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
			Expect(false, $"{fileName} drift; missing from build: [{string.Join("; ", missing)}] new in build: [{string.Join("; ", unexpected)}]");
		}
	}

	private static string[] BuildPatchManifest()
	{
		List<string> lines = [];
		foreach (Type type in PatchTypes())
		{
			SwordPatchAttribute? meta = type.GetCustomAttribute<SwordPatchAttribute>();
			HarmonyMethod merged = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
			foreach (MethodInfo method in PatchMethods(type).OrderBy(method => method.Name, StringComparer.Ordinal))
			{
				lines.Add($"{meta?.Id ?? type.FullName} | {DescribeTarget(merged)} | {DescribePatchMethod(method)}");
			}
		}

		return lines.ToArray();
	}

	private static string DescribeTarget(HarmonyMethod merged)
	{
		string args = merged.argumentTypes == null
			? "*"
			: string.Join(",", merged.argumentTypes.Select(static argument => argument.Name));
		string kind = merged.methodType is null or MethodType.Normal ? string.Empty : $" [{merged.methodType}]";
		return $"{merged.declaringType?.FullName}.{merged.methodName}({args}){kind}";
	}

	private static string DescribePatchMethod(MethodInfo method)
	{
		string kind = method.GetCustomAttribute<HarmonyPostfix>() != null ? "postfix"
			: method.GetCustomAttribute<HarmonyPrefix>() != null ? (method.ReturnType == typeof(bool) ? "prefix?" : "prefix")
			: method.GetCustomAttribute<HarmonyFinalizer>() != null ? "finalizer"
			: "transpiler";
		int priority = method.GetCustomAttribute<HarmonyPriority>()?.info.priority ?? Priority.Normal;
		string[] before = method.GetCustomAttribute<HarmonyBefore>()?.info.before ?? [];
		string[] after = method.GetCustomAttribute<HarmonyAfter>()?.info.after ?? [];
		StringBuilder text = new($"{kind}({priority}");
		if (before.Length > 0)
		{
			text.Append("; before ").Append(string.Join(",", before));
		}

		if (after.Length > 0)
		{
			text.Append("; after ").Append(string.Join(",", after));
		}

		return text.Append(')').ToString();
	}

	private static MethodBase? ResolveTarget(HarmonyMethod merged)
	{
		if (merged.declaringType == null || merged.methodName == null)
		{
			return null;
		}

		return (merged.methodType ?? MethodType.Normal) switch
		{
			MethodType.Getter => AccessTools.PropertyGetter(merged.declaringType, merged.methodName),
			MethodType.Setter => AccessTools.PropertySetter(merged.declaringType, merged.methodName),
			_ => AccessTools.Method(merged.declaringType, merged.methodName, merged.argumentTypes),
		};
	}

	private static string FindTestsSourceDirectory()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "UniversalDominionSword.Tests.csproj")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("tests source directory was not found above " + AppContext.BaseDirectory);
	}
}
