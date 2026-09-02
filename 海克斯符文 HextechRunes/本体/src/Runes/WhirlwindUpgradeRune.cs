using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

public sealed class WhirlwindUpgradeRune : CardUpgradeRuneBase<Whirlwind>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsIroncladPlayer(player);
	}

	internal static void TryDoubleResolvedX(CardModel card, ref int xValue)
	{
		if (xValue < 3
			|| card is not Whirlwind
			|| card.Owner?.GetRelic<WhirlwindUpgradeRune>() == null)
		{
			return;
		}

		xValue *= 2;
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.ResolveEnergyXValue), new Type[0])]
	[HextechPatch("rune.whirlwind", "升级旋风斩", Rune = typeof(WhirlwindUpgradeRune))]
	private static class WhirlwindXValuePatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, ref int __result)
		{
			WhirlwindUpgradeRune.TryDoubleResolvedX(__instance, ref __result);
		}
	}
}
