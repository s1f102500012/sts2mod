using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

public sealed class GrandFinaleUpgradeRune : CardUpgradeRuneBase<GrandFinale>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsSilentPlayer(player);
	}

	internal static bool AllowsPlaying(CardModel card)
	{
		return card is GrandFinale && card.Owner?.GetRelic<GrandFinaleUpgradeRune>() != null;
	}

	internal static async Task PlayUpgradedSafely(PlayerChoiceContext choiceContext, GrandFinale card)
	{
		var combatState = card.CombatState;
		if (combatState == null)
		{
			return;
		}

		await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
			.FromCardCompat(card)
			.TargetingAllOpponents(combatState)
			.WithHitFx(null, null, "blunt_attack.mp3")
			.Execute(choiceContext);
	}

	[HarmonyPatch(typeof(GrandFinale), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	// 升级压轴无视"抽牌堆为空"的出牌条件:只补这张牌自己的 IsPlayable,不碰全局 CanPlay。
	[HarmonyPatch(typeof(GrandFinale), "IsPlayable", MethodType.Getter)]
	[HextechPatch("rune.grand-finale.playable", "升级压轴", Rune = typeof(GrandFinaleUpgradeRune))]
	private static class GrandFinalePlayablePatch
	{
		[HarmonyPostfix]
		private static void Postfix(GrandFinale __instance, ref bool __result)
		{
			if (!__result && GrandFinaleUpgradeRune.AllowsPlaying(__instance))
			{
				__result = true;
			}
		}
	}

	[HextechPatch("rune.grand-finale", "升级压轴", Rune = typeof(GrandFinaleUpgradeRune))]
	private static class GrandFinalePatch
	{
		[HarmonyPrefix]
		private static bool Prefix(GrandFinale __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!GrandFinaleUpgradeRune.AllowsPlaying(__instance))
			{
				return true;
			}

			__result = GrandFinaleUpgradeRune.PlayUpgradedSafely(choiceContext, __instance);
			return false;
		}
	}
}
