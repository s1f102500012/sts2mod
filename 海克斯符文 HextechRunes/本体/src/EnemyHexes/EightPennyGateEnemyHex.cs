namespace HextechRunes;

internal sealed class EightPennyGateEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.EightPennyGate;

	internal override (PileType, CardPilePosition)? ModifyCardPlayResultPileTypeAndPosition(
		HextechEnemyHexContext context,
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		PileType pileType,
		CardPilePosition position)
	{
		if (isAutoPlay
			|| card.Type == CardType.Power
			|| card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState)
		{
			return null;
		}

		int limit = context.TierValue(Kind, 0, 1, 2);
		return TryConsumeExhaustSlot(context.Tracking, card.Owner.NetId, limit)
			? (PileType.Exhaust, position)
			: null;
	}

	internal static bool TryConsumeExhaustSlot(
		HextechMayhemCombatTrackingState tracking,
		ulong playerId,
		int limit)
	{
		if (limit <= 0)
		{
			return false;
		}

		if (tracking.EightPennyGatePlayersTriggeredThisTurn.Add(playerId))
		{
			return true;
		}

		return limit > 1 && tracking.EightPennyGatePlayersTriggeredSecondThisTurn.Add(playerId);
	}
}
