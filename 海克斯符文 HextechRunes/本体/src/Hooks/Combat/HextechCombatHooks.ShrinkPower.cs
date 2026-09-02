using HarmonyLib;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{


	private static bool ShouldReplaceTemporaryShrinkWithPermanent(PowerModel power, decimal offset, Creature? applier)
	{
		return power is ShrinkPower
			&& power.Amount > 0
			&& offset < 0m
			&& power.Owner.Side == CombatSide.Player
			&& applier?.Side == CombatSide.Enemy
			&& power.Owner.GetPowerAmount<ArtifactPower>() <= 0;
	}

	private static async Task<int> ReplaceTemporaryShrinkWithPermanent(
		object? choiceContext,
		PowerModel temporaryShrink,
		decimal permanentOffset,
		Creature? applier,
		CardModel? cardSource,
		bool silent)
	{
		Creature owner = temporaryShrink.Owner;
		await HextechPowerCmdCompat.Remove(temporaryShrink);
		ShrinkPower? permanentShrink = await HextechPowerCmdCompat.Apply<ShrinkPower>(
			choiceContext,
			owner,
			permanentOffset,
			applier,
			cardSource,
			silent);
		return permanentShrink?.Amount ?? 0;
	}

	[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd), nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount), typeof(PlayerChoiceContext), typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool))]
	[HextechPatch("combat.shrink-power", "缩小能力兼容")]
	private static class ModifyAmountPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(
			PlayerChoiceContext choiceContext,
			PowerModel power,
			decimal offset,
			Creature? applier,
			CardModel? cardSource,
			bool silent,
			ref Task<int> __result)
		{
			if (!ShouldReplaceTemporaryShrinkWithPermanent(power, offset, applier))
			{
				return true;
			}

			object? effectiveChoiceContext = null;
			effectiveChoiceContext = choiceContext;

			__result = ReplaceTemporaryShrinkWithPermanent(
				effectiveChoiceContext,
				power,
				offset,
				applier,
				cardSource,
				silent);
			return false;
		}
	}
}
