namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static void CardCanPlayPostfix(CardModel __instance, ref bool __result)
	{
		if (!__result && BlueCandleMedkitRune.AllowsPlaying(__instance))
		{
			__result = true;
			return;
		}

		if (!__result && WhiteHoleCard.AllowsPlaying(__instance))
		{
			__result = true;
			return;
		}

		if (!__result && GrandFinaleUpgradeRune.AllowsPlaying(__instance))
		{
			__result = true;
			return;
		}

		if (__result && IsBlockedByBackToBasics(__instance))
		{
			__result = false;
			return;
		}

		if (__result && KakaRune.BlocksAttack(__instance))
		{
			__result = false;
		}
	}

	private static void CardCanPlayWithReasonPostfix(CardModel __instance, ref bool __result, ref UnplayableReason reason, ref AbstractModel preventer)
	{
		if (!__result)
		{
			if (BlueCandleMedkitRune.AllowsPlaying(__instance))
			{
				reason = default;
				preventer = null!;
				__result = true;
				return;
			}

			if (WhiteHoleCard.AllowsPlaying(__instance))
			{
				reason = default;
				preventer = null!;
				__result = true;
				return;
			}

			if (GrandFinaleUpgradeRune.AllowsPlaying(__instance))
			{
				reason = default;
				preventer = null!;
				__result = true;
			}

			return;
		}

		if (IsBlockedByBackToBasics(__instance, out AbstractModel? backToBasicsPreventer))
		{
			reason = default;
			preventer = backToBasicsPreventer!;
			__result = false;
			return;
		}

		if (KakaRune.BlocksAttack(__instance) && __instance.Owner?.GetRelic<KakaRune>() is KakaRune kakaRune)
		{
			reason = default;
			preventer = kakaRune;
			__result = false;
		}
	}

	private static bool IsBlockedByBackToBasics(CardModel card)
	{
		return IsBlockedByBackToBasics(card, out _);
	}

	private static bool IsBlockedByBackToBasics(CardModel card, out AbstractModel? preventer)
	{
		preventer = null;
		if (card.Owner == null)
		{
			return false;
		}

		// 玩家遗物「回归基本功」:费用 ≥ 3 的牌不可打出。
		BackToBasicsRune? rune = card.Owner.GetRelic<BackToBasicsRune>();
		if (rune != null
			&& !card.EnergyCost.CostsX
			&& GetEnergyCostForCurrentCardPlay(card) >= 3m)
		{
			preventer = rune;
			return true;
		}

		// 敌方海克斯「回归基本功」:每回合打出的牌数达到上限后,其余牌不可再打出。
		if (card.Owner.Creature.Side == CombatSide.Player
			&& card.Owner.Creature.CombatState?.RunState is RunState runState
			&& HextechMayhemModifier.FindIn(runState) is HextechMayhemModifier modifier
			&& modifier.HasActiveMonsterHex(MonsterHexKind.BackToBasics))
		{
			int limit = BackToBasicsEnemyHex.GetTurnCardLimit(modifier);
			int played = modifier.CombatTracking.BackToBasicsCardsPlayedThisTurn.GetValueOrDefault(card.Owner.NetId);
			if (played >= limit)
			{
				preventer = modifier;
				return true;
			}
		}

		return false;
	}
}
