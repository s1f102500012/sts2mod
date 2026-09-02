using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

public sealed class JuggernautUpgradeRune : CardUpgradeRuneBase<Juggernaut>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsIroncladPlayer(player);
	}

	[HarmonyPatch(typeof(JuggernautPower), nameof(JuggernautPower.AfterBlockGained), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel))]
	[HextechPatch("rune.juggernaut", "升级主宰", Rune = typeof(JuggernautUpgradeRune))]
	private static class JuggernautPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(JuggernautPower __instance, Creature creature, decimal amount, ValueProp props, CardModel? cardSource, ref Task __result)
		{
			if (__instance.Owner?.Player?.GetRelic<JuggernautUpgradeRune>() == null)
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.JuggernautUpgradeAfterBlockGained(__instance, creature, amount);
			return false;
		}
	}
}
