using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class DecisionsDecisionsUpgradeRune : CardUpgradeRuneBase<DecisionsDecisions>
{
	private CardModel? _pendingReplayCard;
	private int _pendingRequestedPlayCount;

	protected override bool IsAvailableForCharacter(Player player) => IsRegentPlayer(player);

	internal async Task PlayUpgraded(PlayerChoiceContext choiceContext, DecisionsDecisions card)
	{
		await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
		await CardPileCmd.Draw(choiceContext, card.DynamicVars.Cards.IntValue, card.Owner);

		LocString selectionPrompt = new("cards", $"{card.Id.Entry}.selectionScreenPrompt");
		card.DynamicVars.AddTo(selectionPrompt);
		CardSelectorPrefs prefs = new(selectionPrompt, 1)
		{
			PretendCardsCanBePlayed = true
		};
		CardModel? selectedCard = (await CardSelectCmd.FromHand(
			choiceContext,
			card.Owner,
			prefs,
			CanSelectCard,
			card)).FirstOrDefault();
		if (selectedCard == null)
		{
			return;
		}

		_pendingReplayCard = selectedCard;
		_pendingRequestedPlayCount = card.DynamicVars.Repeat.IntValue;
		try
		{
			// 能力牌首次结算后会离开战斗；在同一次结算内增加次数，才能让所有类型的牌完整重复。
			await CardCmd.AutoPlay(choiceContext, selectedCard, null);
		}
		finally
		{
			_pendingReplayCard = null;
			_pendingRequestedPlayCount = 0;
		}
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (_pendingRequestedPlayCount <= 1 || !ReferenceEquals(card, _pendingReplayCard))
		{
			return playCount;
		}

		return AddRequestedPlayCount(playCount, _pendingRequestedPlayCount);
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (_pendingRequestedPlayCount > 1 && ReferenceEquals(card, _pendingReplayCard))
		{
			_pendingRequestedPlayCount = 0;
		}

		return Task.CompletedTask;
	}

	internal static bool CanSelectCard(CardModel card)
	{
		return CanSelectCard(card.Keywords.Contains(CardKeyword.Unplayable));
	}

	internal static bool CanSelectCard(bool isUnplayable) => !isUnplayable;

	internal static int AddRequestedPlayCount(int playCount, int requestedPlayCount)
	{
		return playCount + Math.Max(0, requestedPlayCount - 1);
	}

	[HarmonyPatch(typeof(DecisionsDecisions), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.decisions.play", "升级抉择", Rune = typeof(DecisionsDecisionsUpgradeRune))]
	private static class DecisionsDecisionsOnPlayPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(
			DecisionsDecisions __instance,
			PlayerChoiceContext choiceContext,
			ref Task __result)
		{
			if (__instance.Owner?.GetRelic<DecisionsDecisionsUpgradeRune>() is not DecisionsDecisionsUpgradeRune rune)
			{
				return true;
			}

			__result = rune.PlayUpgraded(choiceContext, __instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand), typeof(PlayerChoiceContext), typeof(Player), typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>), typeof(AbstractModel))]
	[HextechPatch("rune.decisions.select", "升级抉择", Rune = typeof(DecisionsDecisionsUpgradeRune))]
	private static class DecisionsDecisionsFromHandPatch
	{
		[HarmonyPrefix]
		private static void Prefix(
			AbstractModel source,
			ref Func<CardModel, bool> filter)
		{
			if (source is not DecisionsDecisions card
				|| card.Owner?.GetRelic<DecisionsDecisionsUpgradeRune>() is not DecisionsDecisionsUpgradeRune rune)
			{
				return;
			}

			rune.Flash();
			filter = DecisionsDecisionsUpgradeRune.CanSelectCard;
		}
	}
}
