using MegaCrit.Sts2.Core.Map;

namespace IntegratedStrategyEvents;

internal static class IntegratedStrategyStableRng
{
	private const uint OffsetBasis = 2166136261u;
	private const uint Prime = 16777619u;

	// 0.109 起 Rng.Seed 为 ulong；高低 32 位分别混入，结果保持 uint 稳定种子。
	public static uint CreateSeed(ulong runSeed, string scope, params uint[] values)
	{
		uint hash = HashString(scope);
		hash = Mix(hash, unchecked((uint)runSeed));
		hash = Mix(hash, unchecked((uint)(runSeed >> 32)));
		foreach (uint value in values)
		{
			hash = Mix(hash, value);
		}

		return hash;
	}

	public static uint HashString(string text)
	{
		uint hash = OffsetBasis;
		foreach (char c in text)
		{
			hash ^= c;
			hash *= Prime;
		}

		return hash;
	}

	public static uint HashCoord(MapCoord? coord)
	{
		return coord.HasValue ? HashCoord(coord.Value) : 0xFFFF_FFFFu;
	}

	public static uint HashCoord(MapCoord coord)
	{
		return Mix(unchecked((uint)coord.col), unchecked((uint)coord.row));
	}

	public static uint Mix(uint hash, uint value)
	{
		hash ^= value;
		hash *= Prime;
		hash ^= hash >> 16;
		return hash;
	}
}
