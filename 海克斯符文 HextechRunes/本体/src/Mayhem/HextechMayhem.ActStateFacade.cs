namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	internal int StageCount => _actState.ActCount;

	public int[] EnemyHexCountsByAct => _enemyHexCounts.Snapshot;

	public bool IsActResolved(int actIndex)
	{
		return IsStageResolved(GetSelectionIndexForAct(actIndex));
	}

	public void SetActResolved(int actIndex, bool resolved)
	{
		SetStageResolved(GetSelectionIndexForAct(actIndex), resolved);
	}

	internal bool IsStageResolved(int stageIndex)
	{
		return _actState.IsResolved(stageIndex);
	}

	internal void SetStageResolved(int stageIndex, bool resolved)
	{
		_actState.SetResolved(stageIndex, resolved);
	}

	internal int GetSelectionIndexForAct(int actIndex)
	{
		return actIndex < 0 ? -1 : _runContext.ActSelectionIndexOffset + actIndex;
	}

	internal int GetCurrentActSelectionIndex()
	{
		return GetSelectionIndexForAct(RunState.CurrentActIndex);
	}

	internal int GetCurrentStageIndex()
	{
		return _runContext.ActiveExtraStageIndex ?? GetCurrentActSelectionIndex();
	}

	internal int ActivateExtraStage(string stageKey)
	{
		string scopedKey = $"{_runContext.ActSelectionIndexOffset}:{stageKey}";
		int minimumIndex = Math.Max(
			GetSelectionIndexForAct(RunState.Acts.Count),
			_actState.ActCount);
		int stageIndex = _actState.GetOrCreateExtraStageIndex(scopedKey, minimumIndex);
		_runContext.ActiveExtraStageIndex = stageIndex;
		return stageIndex;
	}

	internal void ClearActiveExtraStage()
	{
		_runContext.ActiveExtraStageIndex = null;
	}

	public bool TryRecoverResolvedActsFromPlayerRelics(string reason, int? currentStageIndex = null)
	{
		int stageIndex = currentStageIndex ?? GetCurrentStageIndex();
		int maxRecoverActIndex = HasRuneSelectionJournalEntriesForAct(stageIndex)
			? stageIndex - 1
			: stageIndex;
		HextechMayhemActRecoveryResult recovery = HextechMayhemActRecovery.RecoverResolvedActs(
			RunState,
			_actState,
			_choiceHistory,
			_hexCountRecoveryBaseline,
			PlayerHexCountsByAct,
			maxRecoverActIndex,
			stageIndex);
		if (recovery.Changed)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] Recovered resolved stages from saved choices/player relics: reason={reason} currentAct={RunState.CurrentActIndex} currentStage={stageIndex} recoverThrough={recovery.RecoverThroughAct} telemetryThrough={recovery.TelemetryRecoverThroughAct} countThrough={recovery.CountRecoverThroughAct} baseline={_hexCountRecoveryBaseline} {_actState.Describe()} counts={DescribePlayerHexCounts()} choices={DescribeTelemetryChoiceCounts()}");
		}

		return recovery.Changed;
	}

	public string DescribeActState()
	{
		return $"offset={_runContext.ActSelectionIndexOffset} activeExtra={_runContext.ActiveExtraStageIndex?.ToString() ?? "none"} {_actState.Describe()}";
	}

	public HextechRarityTier? GetRarityForAct(int actIndex)
	{
		return _actState.GetRarity(actIndex);
	}

	public void SetRarityForAct(int actIndex, HextechRarityTier rarity)
	{
		_actState.SetRarity(actIndex, rarity);
	}

	public MonsterHexKind? GetMonsterHexForAct(int actIndex)
	{
		return _actState.GetMonsterHex(actIndex);
	}

	public IReadOnlyList<MonsterHexKind> GetMonsterHexesForAct(int actIndex)
	{
		return _actState.GetMonsterHexes(actIndex);
	}

	public void SetMonsterHexForAct(int actIndex, MonsterHexKind hex)
	{
		_actState.SetMonsterHex(actIndex, hex);
	}

	public void SetMonsterHexesForAct(int actIndex, IEnumerable<MonsterHexKind> hexes)
	{
		_actState.SetMonsterHexes(actIndex, hexes);
	}

	public void ClearMonsterHexForAct(int actIndex)
	{
		_actState.ClearMonsterHex(actIndex);
	}

	public IReadOnlyList<MonsterHexKind> GetActiveMonsterHexes()
	{
		return _activeMonsterHexCache.Get(_actState, GetCurrentStageIndex(), ShouldRecoverMonsterHexInCombat);
	}

	public IReadOnlyList<MonsterHexKind> GetActiveMonsterHexesBeforeAct(int actIndex)
	{
		return _actState.GetActiveMonsterHexesBeforeAct(actIndex);
	}

	public IReadOnlyList<MonsterHexKind> GetKnownMonsterHexes()
	{
		return _actState.GetKnownMonsterHexes();
	}

	internal IReadOnlyList<IReadOnlyList<MonsterHexKind>> GetMonsterHexRows()
	{
		return _actState.GetMonsterHexRows();
	}

	private bool ShouldRecoverMonsterHexInCombat(int actIndex)
	{
		return actIndex <= GetCurrentStageIndex() && RunState.CurrentRoom is CombatRoom;
	}

	public void ResetForNewRun()
	{
		HextechEnemyHexEffects.ResetAllRunScopedState();
		HextechRunConfigurationSnapshot snapshot = CreateNewRunConfigurationSnapshot();
		_runContext.ResetForNewRun(snapshot.PlayerHexCountsByAct, snapshot.EnemyHexCountsByAct);
		SetRunConfigurationSnapshot(snapshot, "new run");
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Reset for new run: playerCounts={string.Join(",", PlayerHexCountsByAct)} enemyCounts={string.Join(",", EnemyHexCountsByAct)} playerConfigDisabled={PlayerRuneConfigDisabledIds.Count}");
	}

	public void ResetForEndlessLoop(string reason)
	{
		HextechEnemyHexEffects.ResetAllRunScopedState();
		_runContext.ResetForEndlessLoop(HextechMayhemActRecovery.GetMinimumPlayerHexCount(RunState));
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Reset for endless loop: reason={reason} baseline={_hexCountRecoveryBaseline} strengthTierFloor={_monsterHexStrengthTierFloor} enemyCounts={string.Join(",", EnemyHexCountsByAct)} counts={DescribePlayerHexCounts()} {_actState.Describe()}");
		HextechRunLifecycleHooks.HandleEndlessLoopReset(this, reason);
	}

	public void DebugSetOnlyMonsterHex(int actIndex, MonsterHexKind hex, HextechRarityTier rarity)
	{
		_runContext.ResetForDebugMonsterHex(actIndex, hex, rarity);
		SetRunConfigurationSnapshot(HextechRuneConfiguration.GetSnapshot(), "debug set monster hex");
	}

	public bool DebugAddMonsterHex(MonsterHexKind hex)
	{
		return _actState.AddCarriedMonsterHex(hex);
	}

	public bool DebugRemoveMonsterHex(MonsterHexKind hex)
	{
		return _actState.RemoveMonsterHexEverywhere(hex);
	}

	public bool HasActiveMonsterHex(MonsterHexKind hex)
	{
		return _activeMonsterHexCache.Contains(_actState, GetCurrentStageIndex(), ShouldRecoverMonsterHexInCombat, hex);
	}

	public int GetMonsterHexStrengthTier(MonsterHexKind hex)
	{
		return GetMonsterHexStrengthTierForAct(hex, RunState.CurrentActIndex);
	}

	public int GetMonsterHexStrengthTierForAct(MonsterHexKind hex, int actIndex)
	{
		_ = hex;
		// Enemy hex strength tracks the active act, even for hexes obtained in earlier acts.
		int actStrengthTier = Math.Clamp(actIndex + 1, 1, 3);
		return Math.Max(actStrengthTier, _monsterHexStrengthTierFloor);
	}

	public int GetEnemyHexCountForAct(int actIndex)
	{
		return _enemyHexCounts.GetForAct(actIndex, IsEndlessLoopActive);
	}

	public void SetEnemyHexCountsByActSnapshot(IReadOnlyList<int> counts, string reason)
	{
		_enemyHexCounts.Set(counts);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyHexCountsByAct snapshot set: reason={reason} counts={string.Join(",", EnemyHexCountsByAct)}");
	}

	internal bool IncrementEnemyTezcatarasMercyCombatCounter(int interval)
	{
		_enemyTezcatarasMercyCombatCounter++;
		if (_enemyTezcatarasMercyCombatCounter < interval)
		{
			return false;
		}

		_enemyTezcatarasMercyCombatCounter = 0;
		return true;
	}
}
