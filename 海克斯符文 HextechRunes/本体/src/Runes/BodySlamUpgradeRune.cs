using MegaCrit.Sts2.Core.CardSelection;
namespace HextechRunes;

public sealed class BodySlamUpgradeRune : CardUpgradeRuneBase<BodySlam>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsIroncladPlayer(player);
	}

	internal async Task PlayUpgraded(PlayerChoiceContext choiceContext, BodySlam card, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);

		AttackCommand attackCommand = DamageCmd.Attack(card.DynamicVars.CalculatedDamage)
			.FromCardCompat(card, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3");
		await attackCommand.Execute(choiceContext);

		int block = attackCommand.Results
			.SelectMany(static results => results)
			.Sum(static result => CalculateFisticuffsBlock(result.TotalDamage, result.OverkillDamage));
		Flash();
		await CreatureCmd.GainBlock(card.Owner.Creature, block, ValueProp.Move, cardPlay);
	}

	internal static int CalculateFisticuffsBlock(int totalDamage, int overkillDamage)
	{
		return totalDamage + overkillDamage;
	}

	[HarmonyPatch(typeof(BodySlam), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.body-slam", "升级铁山靠", Rune = typeof(BodySlamUpgradeRune))]
	private static class BodySlamPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(
			BodySlam __instance,
			PlayerChoiceContext choiceContext,
			CardPlay cardPlay,
			ref Task __result)
		{
			if (__instance.Owner?.GetRelic<BodySlamUpgradeRune>() is not BodySlamUpgradeRune rune)
			{
				return true;
			}

			__result = rune.PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
