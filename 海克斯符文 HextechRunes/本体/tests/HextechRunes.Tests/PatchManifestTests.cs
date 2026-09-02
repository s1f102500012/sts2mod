using System.Reflection;
using System.Text;
using HarmonyLib;
using HextechRunes;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static string PatchManifestFileName => $"patch_manifest.{ModInfo.TargetGameVersion}.txt";

	/// <summary>
	/// 补丁清单快照:每个补丁类的 id、目标(声明类型/方法名/参数/成员种类)、补丁方法种类与优先级。
	/// 目标集合或优先级变化必须同步更新清单(HEXTECH_WRITE_PATCH_MANIFEST=1 重生成),
	/// 这样"多打了一个原版方法"或"执行序被改"都会在测试里显形,而不是在玩家的联机里。
	/// </summary>
	private static void PatchManifestMatchesCheckedInList()
	{
		string manifestPath = Path.Combine(AppContext.BaseDirectory, PatchManifestFileName);
		string[] actual = BuildPatchManifest();
		if (Environment.GetEnvironmentVariable("HEXTECH_WRITE_PATCH_MANIFEST") == "1")
		{
			string sourcePath = Path.Combine(FindTestsSourceDirectory(), PatchManifestFileName);
			File.WriteAllText(sourcePath, "# 由 HEXTECH_WRITE_PATCH_MANIFEST=1 生成;每行 = 补丁类 id | 目标 | 补丁方法(优先级)。\n" + string.Join("\n", actual) + "\n", Encoding.UTF8);
			Console.WriteLine($"patch manifest written: {sourcePath}");
		}

		Expect(File.Exists(manifestPath), $"{PatchManifestFileName} should exist at {manifestPath} (run with HEXTECH_WRITE_PATCH_MANIFEST=1 to generate)");
		string[] expected = File.ReadAllLines(manifestPath)
			.Select(static line => line.Trim())
			.Where(static line => line.Length > 0 && !line.StartsWith('#'))
			.ToArray();

		string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
		string[] unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
		Expect(
			missing.Length == 0 && unexpected.Length == 0,
			$"patch manifest drift; missing from build: [{string.Join("; ", missing)}] new in build: [{string.Join("; ", unexpected)}]");
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
				&& type.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(Harmony)]) != null;
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
			foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
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

				int priority = method.GetCustomAttribute<HarmonyPriority>()?.info.priority ?? Priority.Normal;
				string skip = kind == "prefix" && method.ReturnType == typeof(bool) ? "?" : string.Empty;
				kinds.Add($"{kind}{skip}({priority})");
			}

			kinds.Sort(StringComparer.Ordinal);
			lines.Add($"{id} | {target} | {string.Join(" ", kinds)}");
		}

		lines.Sort(StringComparer.Ordinal);
		return lines.ToArray();
	}

	private static string DescribePatchTarget(HarmonyMethod merged, Type patchType)
	{
		if (merged.declaringType == null && merged.methodName == null)
		{
			bool hasTargetMethod = patchType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
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
