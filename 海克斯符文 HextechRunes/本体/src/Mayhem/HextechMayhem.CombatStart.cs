namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private async Task ApplyCombatStartEnemyHexes(CombatRoom room)
	{
		await ApplyMonsterCombatStartHexes(room);
		await ApplyCombatStartPlayerDebuffHexes(room);
	}

	private async Task ApplyMonsterCombatStartHexes(CombatRoom room)
	{
		IReadOnlyList<Creature> enemies = HextechCombatCreatureHelper.GetAliveEnemies(room.CombatState);
		if (enemies.Count == 0)
		{
			return;
		}

		enemies = enemies
			.Where(static enemy => !ShouldDeferInitialBossStartHexes(enemy))
			.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		foreach (Creature enemy in enemies)
		{
			await ApplyMonsterCombatStartHexesToEnemy(enemy, room);
		}
	}

	private async Task ApplyMonsterCombatStartHexesToEnemy(
		Creature enemy,
		CombatRoom room)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.ApplyCombatStartToEnemy(context, enemy, room));
	}

	private async Task ApplyCombatStartPlayerDebuffHexes(CombatRoom room)
	{
		IReadOnlyList<Creature> players = HextechCombatCreatureHelper.GetAlivePlayerSideCreatures(room.CombatState);
		if (players.Count == 0)
		{
			return;
		}

		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.ApplyCombatStartPlayerDebuffs(context, room, players));
	}
}
