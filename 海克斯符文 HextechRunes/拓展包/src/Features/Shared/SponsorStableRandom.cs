using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

// 运行种子稳定哈希:不消耗共享 RNG(RunState.Rng.* / PlayerRng.*),也不用 GD.Randi();
// 两端只要种子、幕、层、盐一致就得到同一结果,因此可以在两个联机客户端对称执行。
//
// 算法与盐拼接顺序原样抽自 MiracleEvent.StableRoll(0.9.x 起在用),神迹事件的历史结果必须逐位不变:
// 拼接顺序固定为 StringSeed | "|act:" | act | "|floor:" | floor | ("|" + part)... ,
// 主体是 FNV-1a(64),末尾做一次 MurmurHash3 式最终混淆。不要改动本文件的常量与顺序。
internal static class SponsorStableRandom
{
	// 只负责从 runState 取三个字段,随机核心在 Hash 里(纯函数,便于单元测试)。
	internal static int Roll(Player owner, int count, params string?[] saltParts)
	{
		RunState runState = (RunState)owner.RunState;
		return (int)(Hash(runState.Rng.StringSeed, runState.CurrentActIndex, runState.TotalFloor, saltParts) % (ulong)count);
	}

	internal static ulong Hash(string? stringSeed, int act, int floor, params string?[] saltParts)
	{
		ulong hash = 14695981039346656037UL;
		AddHashPart(ref hash, stringSeed);
		AddHashPart(ref hash, "|act:");
		AddHashPart(ref hash, act.ToString());
		AddHashPart(ref hash, "|floor:");
		AddHashPart(ref hash, floor.ToString());
		foreach (string? part in saltParts)
		{
			AddHashPart(ref hash, "|");
			AddHashPart(ref hash, part);
		}

		unchecked
		{
			hash ^= hash >> 33;
			hash *= 0xff51afd7ed558ccdUL;
			hash ^= hash >> 33;
			hash *= 0xc4ceb9fe1a85ec53UL;
			hash ^= hash >> 33;
		}

		return hash;
	}

	private static void AddHashPart(ref ulong hash, string? value)
	{
		if (value == null)
		{
			return;
		}

		foreach (char ch in value)
		{
			hash ^= ch;
			hash *= 1099511628211UL;
		}
	}
}
