namespace HextechRunes;

internal sealed class MadScientistEnemyHex : HextechEnemyHexEffect, IHextechEnemyMaxHpCoefficientProvider
{
	internal override MonsterHexKind Kind => MonsterHexKind.MadScientist;

	internal override int PersistentOrder => 40;

	internal override async Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers)
	{
		if (creature.CombatId == null
			|| !HextechCombatProcTracker.TryMarkPersistentHexApplied(context.Tracking.MadScientistApplied, creature, replayOneShotPowers))
		{
			return;
		}

		await context.Modifier.ReapplyMonsterMaxHpCoefficients(creature, maxHpBaseOverride);
	}

	public decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature)
	{
		return -context.TierValue(Kind, 0.30m, 0.15m, 0.00m);
	}

	internal override async Task AfterEnemyDamageReceived(HextechEnemyHexContext context, Creature target, uint combatId, DamageResult result, Creature? dealer, CardModel? cardSource)
	{
		if (!target.IsAlive || target.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		Player? player = ResolveDazedTarget(context, combatState, dealer, cardSource);
		if (player == null)
		{
			return;
		}

		CardModel dazed = combatState.CreateCard<Dazed>(player);
		await HextechCardGeneration.AddGeneratedCardToCombat(
			dazed,
			PileType.Discard,
			addedByPlayer: false,
			position: CardPilePosition.Top);
	}

	private static Player? ResolveDazedTarget(HextechEnemyHexContext context, HextechCombatState combatState, Creature? dealer, CardModel? cardSource)
	{
		Player? cardOwner = cardSource?.Owner;
		if (cardOwner?.Creature.IsAlive == true && cardOwner.Creature.CombatState == combatState)
		{
			return cardOwner;
		}

		Player? dealerPlayer = dealer?.Player;
		if (dealerPlayer?.Creature.IsAlive == true && dealerPlayer.Creature.CombatState == combatState)
		{
			return dealerPlayer;
		}

		List<Player> alivePlayers = context.GetAlivePlayerSideCreatures(combatState)
			.Select(static creature => creature.Player)
			.OfType<Player>()
			.ToList();
		return alivePlayers.Count == 1 ? alivePlayers[0] : null;
	}
}
