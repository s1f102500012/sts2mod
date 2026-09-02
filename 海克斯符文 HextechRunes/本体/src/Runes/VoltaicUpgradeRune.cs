using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

public sealed class VoltaicUpgradeRune : CardUpgradeRuneBase<Voltaic>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsDefectPlayer(player);
	}

	internal static bool ShouldUseUpgradedPlay(CardModel card)
	{
		return card is Voltaic && card.Owner?.GetRelic<VoltaicUpgradeRune>() != null;
	}

	internal static async Task PlayUpgraded(PlayerChoiceContext choiceContext, Voltaic card, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);

		List<OrbChanneledEntry> entries = CombatManager.Instance.History.Entries
			.OfType<OrbChanneledEntry>()
			.Where(entry => entry.Actor.Player == card.Owner)
			.ToList();
		if (entries.Count == 0)
		{
			return;
		}

		card.Owner.GetRelic<VoltaicUpgradeRune>()?.Flash();
		foreach (OrbChanneledEntry entry in entries)
		{
			switch (entry.Orb)
			{
				case LightningOrb:
					await OrbCmd.Channel<LightningOrb>(choiceContext, card.Owner);
					break;
				case FrostOrb:
					await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);
					break;
				case DarkOrb:
					await OrbCmd.Channel<DarkOrb>(choiceContext, card.Owner);
					break;
				case PlasmaOrb:
					await OrbCmd.Channel<PlasmaOrb>(choiceContext, card.Owner);
					break;
				case GlassOrb:
					await OrbCmd.Channel<GlassOrb>(choiceContext, card.Owner);
					break;
			}
		}
	}

	[HarmonyPatch(typeof(Voltaic), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.voltaic", "升级伏特", Rune = typeof(VoltaicUpgradeRune))]
	private static class VoltaicPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Voltaic __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!VoltaicUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				return true;
			}

			__result = VoltaicUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
