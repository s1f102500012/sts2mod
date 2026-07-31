using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private readonly HextechMayhemRunContext _runContext = new();
	private HextechMayhemActState _actState => _runContext.ActState;
	private HextechMayhemCombatTrackingState _combatTracking => _runContext.CombatTracking;
	private HextechMayhemChoiceHistoryState _choiceHistory => _runContext.ChoiceHistory;
	private HextechActiveMonsterHexCache _activeMonsterHexCache => _runContext.ActiveMonsterHexCache;
	private HextechPlayerHexCountState _playerHexCounts => _runContext.PlayerHexCounts;
	private HextechEnemyHexCountState _enemyHexCounts => _runContext.EnemyHexCounts;
	private int _hexCountRecoveryBaseline
	{
		get => _runContext.HexCountRecoveryBaseline;
		set => _runContext.HexCountRecoveryBaseline = value;
	}

	private int _monsterHexStrengthTierFloor
	{
		get => _runContext.MonsterHexStrengthTierFloor;
		set => _runContext.MonsterHexStrengthTierFloor = value;
	}

	private int _enemyTezcatarasMercyCombatCounter
	{
		get => _runContext.EnemyTezcatarasMercyCombatCounter;
		set => _runContext.EnemyTezcatarasMercyCombatCounter = value;
	}

	private bool _hostUsesBetterMultiplayerScaling
	{
		get => _runContext.HostUsesBetterMultiplayerScaling;
		set => _runContext.HostUsesBetterMultiplayerScaling = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedRarityByAct
	{
		get => _actState.SavedRarityByAct;
		set => _actState.SavedRarityByAct = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedMonsterHexByAct
	{
		get => _actState.SavedMonsterHexByAct;
		set => _actState.SavedMonsterHexByAct = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedMonsterHexesByActJson
	{
		get => _actState.SavedMonsterHexesByActJson;
		set => _actState.SavedMonsterHexesByActJson = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedCarriedMonsterHexes
	{
		get => _actState.SavedCarriedMonsterHexes;
		set => _actState.SavedCarriedMonsterHexes = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedResolvedActs
	{
		get => _actState.SavedResolvedActs;
		set => _actState.SavedResolvedActs = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedMapLengthReducedActs
	{
		get => _actState.SavedMapLengthReducedActs;
		set => _actState.SavedMapLengthReducedActs = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedPlayerHexCountsByAct
	{
		get => _playerHexCounts.Snapshot;
		set => _playerHexCounts.Set(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedEnemyHexCountsByAct
	{
		get => _enemyHexCounts.Snapshot;
		set => _enemyHexCounts.Set(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedHextechRunConfigurationSnapshotJson
	{
		get => SerializeRunConfigurationSnapshot();
		set => RestoreRunConfigurationSnapshot(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedPlayerRuneConfigDisabledIdsJson
	{
		get => SerializePlayerRuneConfigDisabledIds();
		set => RestorePlayerRuneConfigDisabledIds(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedTelemetryChoicesJson
	{
		get => _choiceHistory.SavedTelemetryChoicesJson;
		set => _choiceHistory.SavedTelemetryChoicesJson = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedSeenPlayerRuneIdsJson
	{
		get => _choiceHistory.SavedSeenPlayerRuneIdsJson;
		set => _choiceHistory.SavedSeenPlayerRuneIdsJson = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedRuneSelectionJournalJson
	{
		get => _runContext.RuneSelectionJournal.Serialize();
		set => _runContext.RuneSelectionJournal.Restore(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedHexCountRecoveryBaseline
	{
		get => _hexCountRecoveryBaseline;
		set => _hexCountRecoveryBaseline = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedMonsterHexStrengthTierFloor
	{
		get => _monsterHexStrengthTierFloor;
		set => _monsterHexStrengthTierFloor = Math.Clamp(value, 0, 3);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedCombatTrackingJson
	{
		get => _combatTracking.Serialize();
		set => _combatTracking.Restore(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedEnemyTezcatarasMercyCombatCounter
	{
		get => _enemyTezcatarasMercyCombatCounter;
		set => _enemyTezcatarasMercyCombatCounter = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedHextechHostUsesBetterMultiplayerScaling
	{
		get => _hostUsesBetterMultiplayerScaling;
		set => _hostUsesBetterMultiplayerScaling = value;
	}

	// 模组总开关的本局冻结值。空字符串=未冻结(默认开启);"True"/"False"=已冻结。
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedModActiveForRun
	{
		get => _runContext.ModActiveForRun?.ToString() ?? string.Empty;
		set => _runContext.ModActiveForRun = string.IsNullOrEmpty(value)
			? null
			: (bool.TryParse(value, out bool parsed) ? parsed : null);
	}

	public override LocString Title => new("modifiers", "HEXTECH_MAYHEM.title");

	public override LocString Description => new("modifiers", "HEXTECH_MAYHEM.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/prismaticForge.png";

	public override IEnumerable<IHoverTip> HoverTips => [];

	public RunState ActiveRunState => RunState;

	internal HextechMayhemCombatTrackingState CombatTracking => _combatTracking;

	internal bool IsEndlessLoopActive => _runContext.IsEndlessLoopActive;

	internal bool HostUsesBetterMultiplayerScaling
	{
		get => _hostUsesBetterMultiplayerScaling;
		set => _hostUsesBetterMultiplayerScaling = value;
	}
}
