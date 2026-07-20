using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 升级:打击/防御(重做)——放开基础打击/防御的升级上限,让原版的多级机制自己工作:
/// MaxUpgradeLevel&gt;1 时,标题原生显示"打击+n"、OnUpgrade 每级 +3、IsUpgradable 常开。
/// 无主上下文(存档反序列化时 Owner 尚未指定)也放行,否则 CurrentUpgradeLevel setter 的
/// 上限校验会在读档回放升级等级时抛异常炸档;没有对应符文的持有者保持上限 1,
/// 等级 1 显示仍是原版的"打击+"。
/// </summary>
internal static class HextechStarterUpgradeHooks
{
	// 上限护栏(玩家实报 3003 伤害事故):第三方 mod 存在「while IsUpgradable 升到满」式逻辑,
	// 上限 999 会被一口气拉满(6+3×999=3003)。战后升级每场只 +1,一局到不了 30,
	// 玩家对封顶无感;即便再被恶性循环拉满,伤害也只到 6+90,不再是团灭级数值。
	private const int UpgradeLevelCap = 30;

	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CardModel), "get_MaxUpgradeLevel", BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechStarterUpgradeHooks), nameof(MaxUpgradeLevelPostfix)));
	}

	private static void MaxUpgradeLevelPostfix(CardModel __instance, ref int __result)
	{
		if (__result != 1)
		{
			return;
		}

		// 卡牌图鉴/预览等场景传入的是 canonical 实例:其 Owner getter 直接抛
		// CanonicalModelException,会把整个图鉴 InitGrid 打断(卡牌全堆左上角,玩家实报,
		// 第三方 mod 的基础打击变体同样命中判定)。canonical 卡不属于任何玩家,保持原上限。
		if (__instance.IsCanonical)
		{
			return;
		}

		bool isStrike = StrikeUpgradeRune.IsBasicStrike(__instance);
		bool isDefend = !isStrike && DefendUpgradeRune.IsBasicDefend(__instance);
		if (!isStrike && !isDefend)
		{
			return;
		}

		Player? owner = __instance.Owner;
		if (owner == null)
		{
			// 反序列化/无主上下文:放行到「当前等级+1」与护栏上限的较大值——回放第 k 级时
			// setter 校验需要 Max≥k(level+1 恰好满足);取 max 兼容已被拉爆的历史存档
			// (等级>上限的档也能读回,不炸档)。
			__result = Math.Max(__instance.CurrentUpgradeLevel + 1, UpgradeLevelCap);
			return;
		}

		if ((isStrike && owner.GetRelic<StrikeUpgradeRune>() != null)
			|| (isDefend && owner.GetRelic<DefendUpgradeRune>() != null))
		{
			// 持有者同样兼容历史坏档:等级已超上限时以当前等级为准(不再增长,但可正常游戏)。
			__result = Math.Max(__instance.CurrentUpgradeLevel, UpgradeLevelCap);
		}
	}
}
