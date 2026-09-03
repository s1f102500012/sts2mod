using System.Reflection;
using System.Text;
using HarmonyLib;

namespace HextechRunesSponsorPack;

/// <summary>
/// 把本模组当前生效的补丁导出成文本表(目标 + 签名、种类、优先级、同目标执行序、补丁方法名),
/// 供重构前后 diff。<see cref="SponsorPatcher.DumpIfRequested"/> 是它唯一的调用方。
/// </summary>
internal static class SponsorPatchTable
{
	// 按 owner 前缀而不是全等收集:让"每功能一个 Harmony id"的旧形态与统一 id 的新形态产出可比的表。
	internal static string Build(string ownerPrefix)
	{
		StringBuilder builder = new();
		foreach (MethodBase method in Harmony.GetAllPatchedMethods().OrderBy(Describe, StringComparer.Ordinal))
		{
			Patches? info = Harmony.GetPatchInfo(method);
			if (info == null)
			{
				continue;
			}

			List<string> lines = [];
			AppendKind(lines, "prefix", info.Prefixes, ownerPrefix);
			AppendKind(lines, "postfix", info.Postfixes, ownerPrefix);
			AppendKind(lines, "transpiler", info.Transpilers, ownerPrefix);
			AppendKind(lines, "finalizer", info.Finalizers, ownerPrefix);
			if (lines.Count == 0)
			{
				continue;
			}

			builder.Append(Describe(method)).Append('\n');
			foreach (string line in lines)
			{
				builder.Append("  ").Append(line).Append('\n');
			}
		}

		return builder.ToString();
	}

	private static string Describe(MethodBase method)
	{
		return $"{method.DeclaringType?.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})";
	}

	private static void AppendKind(List<string> lines, string kind, IReadOnlyCollection<Patch> patches, string ownerPrefix)
	{
		// Harmony 执行序:优先级降序,同优先级按加入序。
		foreach (Patch patch in patches
			.Where(patch => patch.owner.StartsWith(ownerPrefix, StringComparison.Ordinal))
			.OrderByDescending(patch => patch.priority)
			.ThenBy(patch => patch.index))
		{
			string extras = patch.before.Length > 0 ? $" before={string.Join("|", patch.before)}" : string.Empty;
			extras += patch.after.Length > 0 ? $" after={string.Join("|", patch.after)}" : string.Empty;
			extras += kind == "prefix" && patch.PatchMethod.ReturnType == typeof(bool) ? " skip=true" : string.Empty;
			lines.Add($"{kind} priority={patch.priority} {patch.PatchMethod.DeclaringType?.Name}.{patch.PatchMethod.Name}{extras}");
		}
	}
}
