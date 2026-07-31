namespace HextechRunes;

internal sealed class AncientStatueEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.AncientStatue;

	internal override async Task ApplyCombatStartPlayerDebuffs(
		HextechEnemyHexContext context,
		CombatRoom room,
		IReadOnlyList<Creature> players)
	{
		foreach (Creature player in players.Where(static player => player.IsAlive))
		{
			await HextechPlayerSlowPower.ApplyAtZero(player, null, null);
		}
	}

	internal override Task BeforePlayerSideTurnStart(
		HextechEnemyHexContext context,
		HextechCombatState combatState,
		IReadOnlyList<Creature> players)
	{
		HextechPlayerSlowPower.ResetEnemyHexSlowForRound(players);
		return Task.CompletedTask;
	}

	internal override Task AfterCardPlayed(
		HextechEnemyHexContext context,
		PlayerChoiceContext choiceContext,
		CardPlay cardPlay)
	{
		Creature? player = cardPlay.Card.Owner?.Creature;
		if (player?.Side != CombatSide.Player
			|| player.IsDead
			|| player.CombatState?.RunState != context.RunState
			|| player.GetPower<HextechPlayerSlowPower>() == null)
		{
			return Task.CompletedTask;
		}

		return HextechPowerCmdCompat.Apply<HextechPlayerSlowPower>(
			choiceContext,
			player,
			ResolveCardSlowGain(context.GetStrengthTier(Kind)),
			player,
			cardPlay.Card,
			silent: true);
	}

	internal static int ResolveCardSlowGain(int strengthTier)
	{
		return strengthTier switch
		{
			<= 1 => 3,
			2 => 5,
			_ => 8
		};
	}
}
