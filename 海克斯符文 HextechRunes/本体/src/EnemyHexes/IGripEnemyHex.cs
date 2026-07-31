namespace HextechRunes;

internal sealed class IGripEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.IGrip;

	internal override Task AfterCardPlayed(
		HextechEnemyHexContext context,
		PlayerChoiceContext choiceContext,
		CardPlay cardPlay)
	{
		Player? owner = cardPlay.Card.Owner;
		if (cardPlay.IsAutoPlay
			|| !cardPlay.IsFirstInSeries
			|| owner?.Creature.Side != CombatSide.Player
			|| owner.Creature.IsDead
			|| owner.Creature.CombatState?.RunState != context.RunState
			|| owner.PlayerCombatState == null)
		{
			return Task.CompletedTask;
		}

		int amount = context.TierValue(Kind, 0, 1, 2);
		if (TryConsumeFirstCard(context.Tracking, owner.NetId, amount))
		{
			owner.PlayerCombatState.LoseEnergy(amount);
		}

		return Task.CompletedTask;
	}

	internal static bool TryConsumeFirstCard(
		HextechMayhemCombatTrackingState tracking,
		ulong playerId,
		int amount)
	{
		return amount > 0 && tracking.GripPlayersTriggeredThisTurn.Add(playerId);
	}
}
