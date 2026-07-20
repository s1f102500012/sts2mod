namespace HextechRunes;

internal sealed class MindOverMatterEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.MindOverMatter;

	internal override Task AfterCardDrawn(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		Player? owner = card.Owner;
		if (owner?.Creature.Side != CombatSide.Player
			|| owner.Creature.IsDead
			|| owner.Creature.CombatState?.RunState != context.RunState
			|| !TryConsumeFirstDraw(context.Tracking, owner.NetId))
		{
			return Task.CompletedTask;
		}

		if (!card.EnergyCost.CostsX)
		{
			card.EnergyCost.AddThisTurnOrUntilPlayed(
				context.TierValue(Kind, 1, 2, 3),
				reduceOnly: false);
		}

		return Task.CompletedTask;
	}

	internal static bool TryConsumeFirstDraw(HextechMayhemCombatTrackingState tracking, ulong playerNetId)
	{
		return tracking.MindOverMatterPlayersTriggeredThisTurn.Add(playerNetId);
	}
}
