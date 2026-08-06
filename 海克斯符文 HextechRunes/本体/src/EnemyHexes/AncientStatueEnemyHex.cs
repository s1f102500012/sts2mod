namespace HextechRunes;

internal sealed class AncientStatueEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.AncientStatue;

	internal override Task AfterCardPlayed(
		HextechEnemyHexContext context,
		PlayerChoiceContext choiceContext,
		CardPlay cardPlay)
	{
		Creature? player = cardPlay.Card.Owner?.Creature;
		if (player?.Side != CombatSide.Player
			|| player.IsDead
			|| player.CombatState?.RunState != context.RunState)
		{
			return Task.CompletedTask;
		}

		return HextechPowerCmdCompat.Apply<HextechTemporarySlowPower>(
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
