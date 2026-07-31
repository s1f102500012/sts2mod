namespace HextechRunes;

internal sealed class TankEngineEnemyHex : HextechEnemyHexEffect, IHextechEnemyMaxHpCoefficientProvider
{
	internal override MonsterHexKind Kind => MonsterHexKind.TankEngine;

	internal override async Task BeforeEnemySideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players, IReadOnlyList<Creature> enemies)
	{
		foreach (Creature enemy in enemies)
		{
			if (enemy.CombatId is not uint combatId)
			{
				continue;
			}

			int currentRound = combatState.RoundNumber;
			if (context.Tracking.TankEngineLastAppliedRound.GetValueOrDefault(combatId, 0) == currentRound)
			{
				continue;
			}

			bool hadPreviousRound = context.Tracking.TankEngineLastAppliedRound.TryGetValue(
				combatId,
				out int previousRound);
			bool hadPreviousStacks = context.Tracking.TankEngineStacks.TryGetValue(
				combatId,
				out int previousStacks);
			context.Modifier.CaptureMonsterMaxHpCoefficientBase(enemy);
			context.Tracking.TankEngineLastAppliedRound[combatId] = currentRound;
			context.Tracking.TankEngineStacks[combatId] = previousStacks + 1;
			try
			{
				await context.Modifier.ReapplyMonsterMaxHpCoefficients(enemy);
				context.UpdateEnemyScale(enemy);
			}
			catch
			{
				RestoreTrackedValue(
					context.Tracking.TankEngineLastAppliedRound,
					combatId,
					hadPreviousRound,
					previousRound);
				RestoreTrackedValue(
					context.Tracking.TankEngineStacks,
					combatId,
					hadPreviousStacks,
					previousStacks);
				context.Tracking.MonsterMaxHpCoefficientProjected.Remove(combatId);
				throw;
			}
		}
	}

	public decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature)
	{
		int stacks = creature.CombatId is uint combatId
			? context.Tracking.TankEngineStacks.GetValueOrDefault(combatId, 0)
			: 0;
		return Math.Max(0, stacks) * 0.05m;
	}

	private static void RestoreTrackedValue(
		Dictionary<uint, int> values,
		uint combatId,
		bool hadPreviousValue,
		int previousValue)
	{
		if (hadPreviousValue)
		{
			values[combatId] = previousValue;
			return;
		}

		values.Remove(combatId);
	}
}
