namespace HextechRunes;

/// <summary>
/// 药水掉落式的动态掉率:以基础概率起步,每次掉落把概率往下调一档,每次没掉落往上调一档。
/// 只保存相对基础值的偏移(默认 0 = 基础概率),偏移限制在 [0, 100] 概率区间内。
/// </summary>
internal static class HextechDynamicDropChance
{
	public static int ClampOffset(int offset, int baseChance)
	{
		return Math.Clamp(offset, -baseChance, 100 - baseChance);
	}

	public static int CurrentChance(int offset, int baseChance)
	{
		return baseChance + ClampOffset(offset, baseChance);
	}

	/// <summary>本次结算后的新偏移:掉落了就降 <paramref name="step"/>,没掉落就升 <paramref name="step"/>。</summary>
	public static int NextOffset(int offset, int baseChance, int step, bool dropped)
	{
		return ClampOffset(offset + (dropped ? -step : step), baseChance);
	}
}
