namespace HextechRunes;

internal sealed class HextechMayhemRunContext
{
	public HextechMayhemActState ActState { get; } = new();
	public HextechMayhemCombatTrackingState CombatTracking { get; } = new();
	public HextechMayhemChoiceHistoryState ChoiceHistory { get; } = new();
	public HextechRuneSelectionJournalState RuneSelectionJournal { get; } = new();
	public HextechActiveMonsterHexCache ActiveMonsterHexCache { get; } = new();
	public HextechPlayerHexCountState PlayerHexCounts { get; } = new();
	public HextechEnemyHexCountState EnemyHexCounts { get; } = new();
	public HextechPlayerRuneConfigSnapshotState PlayerRuneConfig { get; } = new();
	public HextechRunConfigurationSnapshot? RunConfigurationSnapshot { get; set; }
	public int HexCountRecoveryBaseline { get; set; }
	public int MonsterHexStrengthTierFloor { get; set; }
	public int EnemyTezcatarasMercyCombatCounter { get; set; }
	public bool HostUsesBetterMultiplayerScaling { get; set; }

	// 模组总开关的「本局冻结值」:null=未冻结(默认视为开启)。开局 act1 首次 act-roll 后冻结一次,
	// 之后不变;载入存档时由 SavedProperty 恢复,联机里客户端从房主同步的快照值冻结。
	public bool? ModActiveForRun { get; set; }

	public bool IsEndlessLoopActive => MonsterHexStrengthTierFloor >= 3;

	public void ResetForNewRun(IReadOnlyList<int> playerHexCountsByAct, IReadOnlyList<int> enemyHexCountsByAct)
	{
		PlayerHexCounts.Set(playerHexCountsByAct);
		EnemyHexCounts.Set(enemyHexCountsByAct);
		ModActiveForRun = null;
		ResetProgressState(hexCountRecoveryBaseline: 0, monsterHexStrengthTierFloor: 0);
		ActState.Reset();
		ChoiceHistory.Reset();
		RuneSelectionJournal.Reset();
		ResetCombatTracking();
	}

	public void ResetForEndlessLoop(int hexCountRecoveryBaseline)
	{
		ResetProgressState(hexCountRecoveryBaseline, monsterHexStrengthTierFloor: 3);
		ActState.ResetForEndlessLoop();
		ChoiceHistory.Reset();
		RuneSelectionJournal.Reset();
		ResetCombatTracking();
	}

	public void ResetForDebugMonsterHex(int actIndex, MonsterHexKind hex, HextechRarityTier rarity)
	{
		PlayerHexCounts.ResetToDefault();
		EnemyHexCounts.ResetToDefault();
		ResetProgressState(hexCountRecoveryBaseline: 0, monsterHexStrengthTierFloor: 0);
		ActState.DebugSetOnlyMonsterHex(actIndex, hex, rarity);
		ChoiceHistory.Reset();
		RuneSelectionJournal.Reset();
		ResetCombatTracking();
	}

	public void ResetCombatTracking()
	{
		CombatTracking.Reset();
	}

	private void ResetProgressState(int hexCountRecoveryBaseline, int monsterHexStrengthTierFloor)
	{
		HexCountRecoveryBaseline = hexCountRecoveryBaseline;
		MonsterHexStrengthTierFloor = monsterHexStrengthTierFloor;
		EnemyTezcatarasMercyCombatCounter = 0;
	}
}
