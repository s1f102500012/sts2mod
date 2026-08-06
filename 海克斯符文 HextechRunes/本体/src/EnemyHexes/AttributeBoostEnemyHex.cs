namespace HextechRunes;

internal static class EnemyAttributeBoostValues
{
	internal static decimal GetBonusFraction(MonsterHexKind kind, int strengthTier)
	{
		int tier = Math.Clamp(strengthTier, 1, 3);
		return kind switch
		{
			MonsterHexKind.Stats => tier switch { 1 => 0m, 2 => 0.05m, _ => 0.10m },
			MonsterHexKind.StatsOnStats => tier switch { 1 => 0.05m, 2 => 0.10m, _ => 0.15m },
			MonsterHexKind.StatsOnStatsOnStats => tier switch { 1 => 0.10m, 2 => 0.20m, _ => 0.30m },
			_ => 0m
		};
	}

	internal static decimal GetBonusFraction(MonsterHexKind kind, HextechEnemyHexContext context)
	{
		return GetBonusFraction(kind, context.GetStrengthTier(kind));
	}

	internal static decimal GetMultiplier(MonsterHexKind kind, HextechEnemyHexContext context)
	{
		return 1m + GetBonusFraction(kind, context);
	}

	internal static async Task ApplyPersistent(
		MonsterHexKind kind,
		HextechEnemyHexContext context,
		Creature creature,
		int? maxHpBaseOverride,
		bool replayOneShotPowers)
	{
		HashSet<uint> appliedSet = kind switch
		{
			MonsterHexKind.Stats => context.Tracking.StatsApplied,
			MonsterHexKind.StatsOnStats => context.Tracking.StatsOnStatsApplied,
			MonsterHexKind.StatsOnStatsOnStats => context.Tracking.StatsOnStatsOnStatsApplied,
			_ => throw new InvalidOperationException($"Unsupported enemy attribute boost kind: {kind}")
		};
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(appliedSet, creature, replayOneShotPowers))
		{
			await context.Modifier.ReapplyMonsterMaxHpCoefficients(creature, maxHpBaseOverride);
		}
	}
}
