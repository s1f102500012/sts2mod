using HarmonyLib;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechEnemyCuttingEdgeAlchemistHooks
{
	private const float PotionRewardMultiplier = 0.5f;
	private const float FloatTolerance = 0.000001f;
	private static readonly AccessTools.FieldRef<AbstractOdds, Rng> OddsRngRef =
		AccessTools.FieldRefAccess<AbstractOdds, Rng>("_rng");


	internal static bool ShouldKeepRolledPotion(bool wasForced, float secondaryRoll)
	{
		return wasForced || secondaryRoll < PotionRewardMultiplier;
	}

	internal readonly record struct PotionRollState(bool Active, float OriginalValue);

	// 0.107.1 的 Roll 多一个 AscensionManager 参数(重构前的两参声明在 0.107.1 上从未装上,由声明校验测试抓出)。
#if STS2_107_1
	[HarmonyPatch(typeof(PotionRewardOdds), nameof(PotionRewardOdds.Roll), typeof(Player), typeof(MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager), typeof(RoomType))]
#else
	[HarmonyPatch(typeof(PotionRewardOdds), nameof(PotionRewardOdds.Roll), typeof(Player), typeof(RoomType))]
#endif
	[HextechPatch("enemy-hex.cutting-edge-alchemist", "敌方海克斯:尖端炼金术士")]
	private static class RollPatch
	{
		[HarmonyPrefix]
		private static void Prefix(PotionRewardOdds __instance, Player player, out PotionRollState __state)
		{
			if (!CuttingEdgeAlchemistEnemyHex.IsActiveFor(player))
			{
				__state = default;
				return;
			}

			__state = new PotionRollState(true, __instance.CurrentValue);
		}

		[HarmonyPostfix]
		private static void Postfix(PotionRewardOdds __instance, ref bool __result, PotionRollState __state)
		{
			if (!__state.Active || !__result)
			{
				return;
			}

			bool wasForced = MathF.Abs(__instance.CurrentValue - __state.OriginalValue) <= FloatTolerance;
			if (wasForced)
			{
				return;
			}

			__result = ShouldKeepRolledPotion(wasForced: false, OddsRngRef(__instance).NextFloat());
		}
	}
}
