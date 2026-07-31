namespace HextechRunes;

internal sealed class LeafSlimeEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.LeafSlime;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		if (!context.TryConsumeRoundInterval(
			Kind,
			combatState,
			context.TierValue(Kind, 3, 2, 1)))
		{
			return;
		}

		foreach (Player player in players
			.Where(static creature => !creature.IsDead)
			.Select(static creature => creature.Player)
			.OfType<Player>()
			.OrderBy(static player => player.NetId))
		{
			CardModel slimed = combatState.CreateCard<Slimed>(player);
			await HextechCardGeneration.AddGeneratedCardToCombat(
				slimed,
				PileType.Discard,
				addedByPlayer: false,
				CardPilePosition.Top);
		}
	}
}
