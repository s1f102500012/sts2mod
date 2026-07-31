using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

internal sealed class MysteryEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Mystery;

	internal override async Task AfterPlayerTurnStartLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		if (player.Creature.IsDead
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| combatState.RunState != context.RunState)
		{
			return;
		}

		List<CardModel> candidates = PileType.Hand.GetPile(player).Cards
			.Where(CardTransformUpgradeHelper.CanTransformToRandomCard)
			.OrderBy(HextechStableRandom.CardKey, StringComparer.Ordinal)
			.ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		// 稳定随机抽取不重复的 N 张牌(N 随强度档为 1/1/2),再逐一变化。
		int count = Math.Min(context.TierValue(Kind, 1, 1, 2), candidates.Count);
		List<CardModel> remaining = new(candidates);
		List<CardModel> chosen = new(count);
		for (int i = 0; i < count; i++)
		{
			CardModel pick = HextechStableRandom.Pick(
				remaining,
				(RunState)context.RunState,
				HextechStableRandom.CardKey,
				"enemy-mystery-transform",
				HextechStableRandom.PlayerKey(player),
				combatState.RoundNumber.ToString(),
				i.ToString(),
				HextechStableRandom.CardPileKey(remaining));
			chosen.Add(pick);
			remaining.Remove(pick);
		}

		foreach (CardModel card in chosen)
		{
			int transformOrdinal = HextechCombatProcTracker.ConsumeGlobalProcInCombat(
				context.Tracking,
				string.Join(":", nameof(MysteryEnemyHex), HextechStableRandom.PlayerKey(player)));
			await CardTransformUpgradeHelper.TransformToStableRandom(
				card,
				(RunState)context.RunState,
				"enemy-mystery-transform-replacement",
				transformOrdinal,
				CardPreviewStyle.HorizontalLayout,
				HextechStableRandom.PlayerKey(player),
				combatState.RoundNumber.ToString(),
				HextechStableRandom.CardPileKey(candidates));
		}
	}
}
