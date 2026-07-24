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

	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(PotionRewardOdds), nameof(PotionRewardOdds.Roll), BindingFlags.Public | BindingFlags.Instance, typeof(Player), typeof(RoomType)),
			prefix: new HarmonyMethod(typeof(HextechEnemyCuttingEdgeAlchemistHooks), nameof(RollPrefix)),
			postfix: new HarmonyMethod(typeof(HextechEnemyCuttingEdgeAlchemistHooks), nameof(RollPostfix)));
	}

	private static void RollPrefix(PotionRewardOdds __instance, Player player, out PotionRollState __state)
	{
		if (!CuttingEdgeAlchemistEnemyHex.IsActiveFor(player))
		{
			__state = default;
			return;
		}

		__state = new PotionRollState(true, __instance.CurrentValue);
	}

	private static void RollPostfix(PotionRewardOdds __instance, ref bool __result, PotionRollState __state)
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

	internal static bool ShouldKeepRolledPotion(bool wasForced, float secondaryRoll)
	{
		return wasForced || secondaryRoll < PotionRewardMultiplier;
	}

	internal readonly record struct PotionRollState(bool Active, float OriginalValue);
}
