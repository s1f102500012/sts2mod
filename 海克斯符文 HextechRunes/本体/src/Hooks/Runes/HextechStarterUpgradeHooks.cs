using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 升级:打击/防御(重做)——将基础打击/防御的升级上限放宽到 999,让原版的多级机制自己工作:
/// MaxUpgradeLevel&gt;1 时,标题原生显示"打击+n",OnUpgrade 每级 +3。
/// 反序列化时 Owner 尚未指定,需要临时放行超过上限的历史等级;普通无主卡保持原版上限,
/// 避免第三方的 <c>while (IsUpgradable)</c> 升满逻辑越过 +1 或永不终止。
/// </summary>
internal static class HextechStarterUpgradeHooks
{
	internal const int UpgradeLevelCap = 999;

	private static readonly HextechScopedDepthGuard CardDeserializationGuard = new();


	internal static int ResolveOwnedMaxUpgradeLevel(int currentUpgradeLevel)
	{
		return Math.Max(currentUpgradeLevel, UpgradeLevelCap);
	}

	internal static int ResolveUnownedMaxUpgradeLevel(int currentUpgradeLevel, bool isDeserializing)
	{
		if (!isDeserializing)
		{
			return 1;
		}

		int nextUpgradeLevel = currentUpgradeLevel == int.MaxValue
			? int.MaxValue
			: currentUpgradeLevel + 1;
		return Math.Max(nextUpgradeLevel, UpgradeLevelCap);
	}


	[HarmonyPatch(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel), MethodType.Getter)]
	[HextechPatch("card.starter-upgrade.max-level", "起始牌多重升级")]
	private static class MaxUpgradeLevelPatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, ref int __result)
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
				__result = ResolveUnownedMaxUpgradeLevel(
					__instance.CurrentUpgradeLevel,
					CardDeserializationGuard.IsActive);
				return;
			}

			if ((isStrike && owner.GetRelic<StrikeUpgradeRune>() != null)
				|| (isDefend && owner.GetRelic<DefendUpgradeRune>() != null))
			{
				// 持有者同样兼容历史坏档:等级已超上限时以当前等级为准(不再增长,但可正常游戏)。
				__result = ResolveOwnedMaxUpgradeLevel(__instance.CurrentUpgradeLevel);
			}
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable), typeof(SerializableCard))]
	[HextechPatch("card.starter-upgrade.load", "起始牌多重升级")]
	private static class FromSerializablePatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			CardDeserializationGuard.Enter();
		}

		[HarmonyFinalizer]
		private static Exception? Finalizer(Exception? __exception)
		{
			CardDeserializationGuard.Exit();
			return __exception;
		}
	}
}
