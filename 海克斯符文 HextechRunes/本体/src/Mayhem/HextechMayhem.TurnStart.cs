namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private async Task BeforePlayerSideTurnStart(HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		_combatTracking.PreparePlayerSideTurnStart();
		RefreshPlayerAttackCostDoublingPreviews(players);

		await ApplyToCurrentEnemiesIfNeeded();
		await ApplyDelayedEnemyHealingBlocks(combatState);
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.BeforePlayerSideTurnStart(context, combatState, players));
	}

	private async Task BeforeEnemySideTurnStart(HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		_combatTracking.PrepareEnemySideTurnStart();
		RefreshPlayerAttackCostDoublingPreviews(players);

		IReadOnlyList<Creature> enemies = HextechCombatCreatureHelper.GetAliveEnemies(combatState);
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.BeforeEnemySideTurnStart(context, combatState, players, enemies));
	}
}
