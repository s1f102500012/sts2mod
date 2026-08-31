namespace HextechRunes;

internal static class HextechRuneTargeting
{
	internal static Creature? PickRandomHittableEnemy(
		Player? owner,
		HextechCombatState? combatState,
		string scope,
		params string?[] saltParts)
	{
		if (owner == null || combatState == null)
		{
			return null;
		}

		List<Creature> enemies = combatState.HittableEnemies
			.OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue)
			.ToList();
		if (enemies.Count == 0)
		{
			return null;
		}

		string?[] fullSalt = new string?[saltParts.Length + 2];
		fullSalt[0] = scope;
		fullSalt[1] = HextechStableRandom.PlayerKey(owner);
		Array.Copy(saltParts, 0, fullSalt, 2, saltParts.Length);

		return enemies[HextechStableRandom.Index((RunState)owner.RunState, enemies.Count, fullSalt)];
	}
}
