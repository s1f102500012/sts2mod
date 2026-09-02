using MegaCrit.Sts2.Core.CardSelection;
namespace HextechRunes;

public sealed class WroughtInWarUpgradeRune : CardUpgradeRuneBase<WroughtInWar>
{
	protected override bool IsAvailableForCharacter(Player player) => IsRegentPlayer(player);

	internal async Task PlayUpgraded(PlayerChoiceContext choiceContext, WroughtInWar card, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);

		AttackCommand attackCommand = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
			.FromCardCompat(card, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt");
		await attackCommand.Execute(choiceContext);

		int block = attackCommand.Results
			.SelectMany(static results => results)
			.Sum(static result => CalculateFisticuffsBlock(result.TotalDamage, result.OverkillDamage));
		Flash();
		await CreatureCmd.GainBlock(card.Owner.Creature, block, ValueProp.Move, cardPlay);
		await ForgeCmd.Forge(card.DynamicVars.Forge.IntValue, card.Owner, card);
	}

	internal static int CalculateFisticuffsBlock(int totalDamage, int overkillDamage)
	{
		return totalDamage + overkillDamage;
	}

	[HarmonyPatch(typeof(WroughtInWar), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.wrought-in-war", "升级战火淬炼", Rune = typeof(WroughtInWarUpgradeRune))]
	private static class WroughtInWarPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(
			WroughtInWar __instance,
			PlayerChoiceContext choiceContext,
			CardPlay cardPlay,
			ref Task __result)
		{
			if (__instance.Owner?.GetRelic<WroughtInWarUpgradeRune>() is not WroughtInWarUpgradeRune rune)
			{
				return true;
			}

			__result = rune.PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
