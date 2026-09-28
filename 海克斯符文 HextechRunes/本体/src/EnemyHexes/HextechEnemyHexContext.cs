namespace HextechRunes;

// turnParticipants 只在回合开始/结束钩子里有值：队友的额外回合只带那名玩家重入这些钩子。
internal readonly struct HextechEnemyHexContext(HextechMayhemModifier modifier, IEnumerable<Creature>? turnParticipants = null)
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

	// “每过 N 回合”在 RoundNumber % (N + 1) == 0 时触发；额外回合不推进回合号，按海克斯和回合防重。
	internal bool TryConsumeRoundInterval(
		MonsterHexKind kind,
		HextechCombatState combatState,
		int everyNRounds)
	{
		int roundNumber = combatState.RoundNumber;
		return IsRoundIntervalDue(roundNumber, everyNRounds)
			&& HextechCombatProcTracker.ConsumeGlobalProcInCombat(
				Tracking,
				$"round-once:{kind}:{roundNumber}") <= 0;
	}

	internal static bool IsRoundIntervalDue(int roundNumber, int everyNRounds)
	{
		return everyNRounds > 0
			&& roundNumber > 1
			&& roundNumber % (everyNRounds + 1) == 0;
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

	// 玩家侧回合钩子里作用于"每名玩家"的效果用这个：只取本次真正开始/结束回合的玩家（宠物随主人）。
	internal IReadOnlyList<Creature> GetAlivePlayerSideCreaturesTakingTurn(HextechCombatState combatState)
	{
		return FilterTakingTurn(GetAlivePlayerSideCreatures(combatState), turnParticipants);
	}

	internal static IReadOnlyList<Creature> FilterTakingTurn(IReadOnlyList<Creature> creatures, IEnumerable<Creature>? participants)
	{
		return participants == null
			? creatures
			: creatures.Where(creature => HextechTurnParticipants.Includes(participants, creature)).ToList();
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
