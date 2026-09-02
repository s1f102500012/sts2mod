using System.Reflection;
using System.Text;
using HextechRunes;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static string StaticStateManifestFileName => $"static_state_manifest.{ModInfo.TargetGameVersion}.txt";

	/// <summary>
	/// 可变静态字段清单快照:进程级可变状态是联机分叉与读档串状态的温床,新增一个就必须在这里显形并说明作用域。
	/// 只列非 readonly、非 const 的静态字段;readonly 的缓存/反射句柄不在此列。
	/// 用 HEXTECH_WRITE_PATCH_MANIFEST=1 重生成。
	/// </summary>
	private static void StaticStateManifestMatchesCheckedInList()
	{
		string manifestPath = Path.Combine(AppContext.BaseDirectory, StaticStateManifestFileName);
		string[] actual = BuildStaticStateManifest();
		if (Environment.GetEnvironmentVariable("HEXTECH_WRITE_PATCH_MANIFEST") == "1")
		{
			string sourcePath = Path.Combine(FindTestsSourceDirectory(), StaticStateManifestFileName);
			File.WriteAllText(sourcePath, "# 由 HEXTECH_WRITE_PATCH_MANIFEST=1 生成;每行 = 类型.字段 : 字段类型(可变静态字段)。\n" + string.Join("\n", actual) + "\n", Encoding.UTF8);
			Console.WriteLine($"static state manifest written: {sourcePath}");
		}

		Expect(File.Exists(manifestPath), $"{StaticStateManifestFileName} should exist at {manifestPath} (run with HEXTECH_WRITE_PATCH_MANIFEST=1 to generate)");
		string[] expected = File.ReadAllLines(manifestPath)
			.Select(static line => line.Trim())
			.Where(static line => line.Length > 0 && !line.StartsWith('#'))
			.ToArray();

		string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
		string[] unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
		Expect(
			missing.Length == 0 && unexpected.Length == 0,
			$"static state manifest drift; removed: [{string.Join("; ", missing)}] new mutable statics: [{string.Join("; ", unexpected)}]");
	}

	internal static string[] BuildStaticStateManifest()
	{
		List<string> lines = [];
		foreach (Type type in typeof(ModEntry).Assembly.GetTypes())
		{
			if (type.Name.StartsWith('<'))
			{
				continue;
			}

			foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				if (field.IsInitOnly || field.IsLiteral || field.Name.StartsWith('<'))
				{
					continue;
				}

				lines.Add($"{type.FullName}.{field.Name} : {field.FieldType.Name}");
			}
		}

		lines.Sort(StringComparer.Ordinal);
		return lines.ToArray();
	}
}
