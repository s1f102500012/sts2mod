namespace HextechRunes;

internal readonly struct HextechEnemyHexContext(HextechMayhemModifier modifier)
{
	internal HextechMayhemModifier Modifier => modifier;

	internal RunState RunState => modifier.ActiveRunState;

	internal HextechMayhemCombatTrackingState Tracking => modifier.CombatTracking;

	internal int ScalingPlayerCount => Math.Clamp(RunState.Players.Count, 1, 16);

	internal bool IsActive(MonsterHexKind kind)
	{
		return modifier.HasActiveMonsterHex(kind);
	}

	internal int GetStrengthTier(MonsterHexKind kind)
	{
		return modifier.GetMonsterHexStrengthTier(kind);
	}

	internal int GetStrengthTierForAct(MonsterHexKind kind, int actIndex)
	{
		return modifier.GetMonsterHexStrengthTierForAct(kind, actIndex);
	}

	internal int TierValue(MonsterHexKind kind, int tier1, int tier2, int tier3)
	{
		return GetStrengthTier(kind) switch
		{
			<= 1 => tier1,
			2 => tier2,
			_ => tier3
		};
	}

	internal decimal TierValue(MonsterHexKind kind, decimal tier1, decimal tier2, decimal tier3)
	{
		return GetStrengthTier(kind) switch
		{
			<= 1 => tier1,
			2 => tier2,
			_ => tier3
		};
	}

	internal int TierValueForAct(MonsterHexKind kind, int actIndex, int tier1, int tier2, int tier3)
	{
		return GetStrengthTierForAct(kind, actIndex) switch
		{
			<= 1 => tier1,
			2 => tier2,
			_ => tier3
		};
	}

	internal IReadOnlyList<Creature> GetAliveEnemies(HextechCombatState combatState)
	{
		return HextechCombatCreatureHelper.GetAliveEnemies(combatState);
	}

	internal IReadOnlyList<Creature> GetAlivePlayerSideCreatures(HextechCombatState combatState)
	{
		return HextechCombatCreatureHelper.GetAlivePlayerSideCreatures(combatState);
	}

	internal Task RunGroupedPlayerDebuffBurst(Func<Task> action)
	{
		return modifier.RunGroupedPlayerDebuffBurst(action);
	}

	internal Task TryApplyServantMasterIllusion(Creature creature, Creature? applier, CardModel? cardSource)
	{
		return modifier.TryApplyServantMasterIllusion(creature, applier, cardSource);
	}

	internal void UpdateEnemyScale(Creature creature)
	{
		modifier.UpdateEnemyScale(creature);
	}
}
