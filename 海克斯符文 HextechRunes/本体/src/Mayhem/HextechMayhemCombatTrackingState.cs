namespace HextechRunes;

internal sealed partial class HextechMayhemCombatTrackingState
{
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<uint, int> SlapProcsThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<uint, int> TormentorProcsThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<uint, int> CourageProcsThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<uint, int> BloodPactProcsThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<string, int> PlayerRuneProcsThisTurn = new();
	public readonly Dictionary<string, int> PlayerRuneProcsThisCombat = new();
	public readonly Dictionary<string, int> GlobalProcsThisCombat = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart | CombatTrackingClearPhase.EnemyTurnStart)]
	public readonly Dictionary<uint, int> BloodArmorHpLossThisPlayerTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<uint, int> ClownCollegeProcsThisTurn = new();
	public readonly HashSet<uint> EscapePlanTriggered = new();
	public readonly HashSet<uint> EscapePlanPending = new();
	public readonly HashSet<uint> RepulsorTriggered = new();
	public readonly HashSet<uint> RepulsorPending = new();
	public readonly HashSet<uint> DawnTriggered = new();
	// 敌方濒死狂宴:负血债务(含 key=濒死激活中)与已发放的力量层数(差额补给)。
	public readonly Dictionary<uint, int> NearDeathFeastEnemyDebt = new();
	public readonly Dictionary<uint, int> NearDeathFeastEnemyStrength = new();
	public readonly HashSet<uint> SpeedDemonPending = new();
	public readonly Dictionary<uint, int> DelayedEnemyHealingBlock = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly HashSet<uint> DevilsDanceTriggeredThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly HashSet<uint> FinalFormTriggeredThisTurn = new();
	public readonly HashSet<uint> FeelTheBurnTriggered = new();
	public readonly Dictionary<uint, uint> FeyMagicPendingNoDrawPlayers = new();
	public readonly Dictionary<uint, int> MikaelsBlessingTriggers = new();
	public readonly HashSet<uint> GoliathApplied = new();
	public readonly HashSet<uint> ProtectiveVeilApplied = new();
	public readonly HashSet<uint> ThornmailApplied = new();
	public readonly HashSet<uint> SuperBrainApplied = new();
	public readonly HashSet<uint> AstralBodyApplied = new();
	public readonly HashSet<uint> MadScientistApplied = new();
	public readonly HashSet<uint> UnmovableMountainApplied = new();
	public readonly HashSet<uint> GoldenSpatulaApplied = new();
	public readonly HashSet<uint> StatsApplied = new();
	public readonly HashSet<uint> StatsOnStatsApplied = new();
	public readonly HashSet<uint> StatsOnStatsOnStatsApplied = new();
	public readonly HashSet<uint> DoormakerRealStartApplied = new();
	public readonly Dictionary<uint, int> TestSubjectPhaseStartApplied = new();
	public readonly Dictionary<uint, int> MonsterMaxHpCoefficientBase = new();
	public readonly Dictionary<uint, int> MonsterMaxHpCoefficientProjected = new();
	public readonly Dictionary<uint, int> TankEngineStacks = new();
	public readonly Dictionary<uint, int> TankEngineLastAppliedRound = new();
	public readonly Dictionary<uint, int> ShrinkEngineStacks = new();
	public readonly Dictionary<uint, int> GetExcitedPending = new();
	public readonly HashSet<uint> FeelTheBurnPending = new();
	public readonly HashSet<uint> MountainSoulHasPreviousTurn = new();
	public readonly HashSet<uint> MountainSoulDamagedSinceLastTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.EveryTurnBoundary)]
	public readonly Dictionary<ulong, int> PlayerAttackCardsPlayedThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.EveryTurnBoundary)]
	public readonly Dictionary<ulong, int> BackToBasicsCardsPlayedThisTurn = new();
	public readonly Dictionary<ulong, int> PlayerCardsDrawnThisCombat = new();
	public readonly Dictionary<ulong, int> SwiftAndSafePlayerCardsDrawnThisCombat = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly HashSet<ulong> MindOverMatterPlayersTriggeredThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.EveryTurnBoundary)]
	public readonly Dictionary<uint, int> EnemyPorcupineTemporaryThornsThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<uint, int> EnemyPorcupineTriggersThisTurn = new();
	public readonly HashSet<ulong> VakuuControlledPlayersThisCombat = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly HashSet<ulong> EightPennyGatePlayersTriggeredThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly HashSet<ulong> EightPennyGatePlayersTriggeredSecondThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly Dictionary<ulong, int> InspectExtraDrawsPreventedThisTurn = new();
	// 原版的玩家回合判定从回合开始 hook 起即为 true；单独记录进入出牌阶段前的窗口。
	[CombatTrackingTransient]
	public readonly HashSet<ulong> PlayersAwaitingPlayPhase = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	public readonly HashSet<ulong> GripPlayersTriggeredThisTurn = new();
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart | CombatTrackingClearPhase.PlayerTurnEnd)]
	public int ArcanePunchPlayerAttackCardsPlayed;
	[CombatTrackingClear(CombatTrackingClearPhase.PlayerTurnStart)]
	[CombatTrackingTransient]
	public readonly HashSet<string> MonsterDebuffActionProcKeysThisTurn = new();
	[CombatTrackingTransient]
	public readonly HashSet<string> GroupedPlayerDebuffProcKeys = new();
	[CombatTrackingTransient]
	public string? LastEnemyThresholdTriggerKey;
	[CombatTrackingTransient]
	public bool HandlingMonsterTormentorBurn;
	[CombatTrackingTransient]
	public bool HandlingServantMasterIllusion;
	[CombatTrackingTransient]
	public bool HandlingGroupedPlayerDebuffs;
	public int EnemyProtectiveVeilTurnCounter;

	public void PreparePlayerSideTurnStart()
	{
		HextechMayhemCombatTrackingSerializer.ClearPhase(this, CombatTrackingClearPhase.PlayerTurnStart);
		PlayersAwaitingPlayPhase.Clear();
	}

	public void BeginPlayerTurnStart(IEnumerable<ulong> playerIds)
	{
		PlayersAwaitingPlayPhase.Clear();
		PlayersAwaitingPlayPhase.UnionWith(playerIds);
	}

	public void EnterPlayerPlayPhase(ulong playerId)
	{
		PlayersAwaitingPlayPhase.Remove(playerId);
	}

	public bool IsPlayerTurnStart(ulong playerId)
	{
		return PlayersAwaitingPlayPhase.Contains(playerId);
	}

	public void PreparePlayerSideTurnEnd()
	{
		HextechMayhemCombatTrackingSerializer.ClearPhase(this, CombatTrackingClearPhase.PlayerTurnEnd);
	}

	public void PrepareEnemySideTurnStart()
	{
		// 自增计数器无法用清空标注表达,保留显式;其余字段按 EnemyTurnStart 标注反射清空。
		EnemyProtectiveVeilTurnCounter++;
		PlayersAwaitingPlayPhase.Clear();
		HextechMayhemCombatTrackingSerializer.ClearPhase(this, CombatTrackingClearPhase.EnemyTurnStart);
	}

	public void Reset()
	{
		Clear();
	}
}
