using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

/// <summary>
/// 升级：大奖——大奖的随机牌部分改为:3 张不限费用的随机牌加入手牌,
/// 且在本场战斗内可以免费打出(原版只选 0 费牌)。伤害部分保持原版。
/// 由 HextechPlayerRuneHooks 对 Jackpot.OnPlay 做 prefix 替换驱动。
/// </summary>
public sealed class JackpotUpgradeRune : CardUpgradeRuneBase<Jackpot>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return true;
	}

	internal static bool ShouldUseUpgradedPlay(Jackpot card)
	{
		return card.Owner?.GetRelic<JackpotUpgradeRune>() != null;
	}

	internal static async Task OnPlayUpgraded(Jackpot card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		Player owner = card.Owner;
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
		await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
			.FromCardCompat(card, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);

		if (owner.Creature.CombatState == null)
		{
			return;
		}

		owner.GetRelic<JackpotUpgradeRune>()?.Flash();
		// 不限费用的随机牌(原版此处过滤 0 费);rng 沿用原版的 CombatCardGeneration,两端一致。
		IEnumerable<CardModel> generated = CardFactory.GetForCombat(
			owner,
			owner.Character.CardPool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			card.DynamicVars.Cards.IntValue,
			owner.RunState.Rng.CombatCardGeneration);
		foreach (CardModel generatedCard in generated)
		{
			if (card.IsUpgraded)
			{
				CardCmd.Upgrade(generatedCard);
			}

			generatedCard.SetToFreeThisCombat();
			await HextechCardGeneration.AddGeneratedCardToCombat(generatedCard, PileType.Hand, addedByPlayer: true);
		}
	}

	[HarmonyPatch(typeof(Jackpot), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.jackpot", "升级大奖", Rune = typeof(JackpotUpgradeRune))]
	private static class JackpotPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(Jackpot __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!JackpotUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				return true;
			}

			__result = JackpotUpgradeRune.OnPlayUpgraded(__instance, choiceContext, cardPlay);
			return false;
		}
	}
}
