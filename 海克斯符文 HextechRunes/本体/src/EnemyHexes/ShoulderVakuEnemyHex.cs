namespace HextechRunes;

internal sealed class ShoulderVakuEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ShoulderVaku;

	internal override Task AfterAutoPrePlayPhaseEnteredLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return TryControlSecondTurn(context, player);
	}

	private static async Task TryControlSecondTurn(HextechEnemyHexContext context, Player player)
	{
		if (player.Creature.IsDead
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| combatState.RunState != context.RunState
			|| combatState.RoundNumber != 2
			|| !context.Tracking.VakuuControlledPlayersThisCombat.Add(player.NetId))
		{
			return;
		}

		int cardsPlayed = await VakuuTurnController.AutoPlayPlayableHand(player);
		VakuuTurnController.PlayLineIfCardsPlayed(player, cardsPlayed);
	}
}
