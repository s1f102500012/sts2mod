using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed class HextechEnemyHexAdjustmentOptions
{
	public MonsterHexKind? InitialHex { get; init; }

	public IReadOnlyList<MonsterHexKind> InitialHexes { get; init; } = [];

	public IReadOnlyList<MonsterHexKind> ExcludedHexes { get; init; } = [];

	public bool ControlsEnabled { get; init; }

	public Func<IReadOnlyList<MonsterHexKind?>, int, int, MonsterHexKind?>? RerollFunc { get; init; }

	public int RerollLimit { get; init; } = HextechRuneConfiguration.GetDefaultMonsterHexRerollLimit();

	public Action<IReadOnlyList<MonsterHexKind?>, IReadOnlyList<int>>? Changed { get; init; }

	public Action<HextechRuneSelectionScreen>? ScreenCreated { get; init; }
}

internal enum HextechSelectionMetadataMode
{
	PlayerRune,
	Forge
}

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private readonly TaskCompletionSource<IEnumerable<RelicModel>> _completionSource = new();
	private readonly Func<IReadOnlyList<RelicModel>, int, int, IReadOnlyList<RelicModel>>? _rerollFunc;
	private readonly Func<IReadOnlyList<MonsterHexKind?>, int, int, MonsterHexKind?>? _enemyHexRerollFunc;
	private readonly Action<IReadOnlyList<MonsterHexKind?>, IReadOnlyList<int>>? _enemyHexChanged;
	private readonly int _playerRuneRerollLimit;
	private readonly int _enemyHexRerollLimit;
	private readonly string? _titleOverride;
	private readonly HextechSelectionMetadataMode _metadataMode;
	private List<RelicModel> _relics;
	private readonly List<MonsterHexKind?> _monsterHexKinds = [];
	private readonly List<MonsterHexKind?> _monsterHexBeforeRemoval = [];
	private readonly List<int> _enemyHexRerollCounts = [];
	private readonly string _rarityKey;
	private readonly List<Button> _holders = new();
	private readonly List<Button> _rerollButtons = new();
	private readonly List<int> _playerRuneRerollCounts = new();
	private readonly List<int> _rerollHistory = new();
	private readonly bool _enemyHexControlsEnabled;
	private HBoxContainer? _cardsRow;
	private VBoxContainer? _enemyPreviewHost;
	private MegaLabel? _statusLabel;
	private bool _choiceLocked;
	private bool _blockMapUntilDismissed;
	private bool _restoreAfterMapReopenQueued;
	private bool _closed;
	private bool _mapPreviewActive;
	private bool _mapButtonForceEnabled;
	private MegaLabel? _mapPreviewHint;
	private bool _selectionConfirmGuardStarted;
	private ulong _selectionConfirmGuardEndsAtMsec;

	public NetScreenType ScreenType => NetScreenType.Rewards;

	public bool UseSharedBackstop => true;

	public Control? DefaultFocusedControl => _holders.FirstOrDefault();

	public bool RequestedReroll => false;

	public IReadOnlyList<RelicModel> CurrentRelics => _relics;

	public IReadOnlyList<int> RerollHistory => _rerollHistory;

	public MonsterHexKind? CurrentMonsterHex
	{
		get
		{
			IReadOnlyList<MonsterHexKind> currentMonsterHexes = CurrentMonsterHexes;
			return currentMonsterHexes.Count > 0 ? currentMonsterHexes[0] : null;
		}
	}

	public IReadOnlyList<MonsterHexKind> CurrentMonsterHexes => _monsterHexKinds
		.Where(static hex => hex.HasValue)
		.Select(static hex => hex!.Value)
		.ToArray();

	public IReadOnlyList<MonsterHexKind?> CurrentMonsterHexSlots => _monsterHexKinds.ToArray();

	public bool EnemyHexRemoved => _monsterHexKinds.Count > 0 && _monsterHexKinds.All(static hex => !hex.HasValue);

	public IReadOnlyList<int> EnemyHexRerollCounts => _enemyHexRerollCounts.ToArray();

	public int EnemyHexRerollCount => _enemyHexRerollCounts.Sum();

	private HextechRuneSelectionScreen(
		IReadOnlyList<RelicModel> relics,
		RelicModel? monsterHexRelic,
		Func<IReadOnlyList<RelicModel>, int, int, IReadOnlyList<RelicModel>>? rerollFunc,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions,
		int playerRuneRerollLimit,
		string? titleOverride,
		HextechSelectionMetadataMode metadataMode)
	{
		_relics = relics.ToList();
		_rerollFunc = rerollFunc;
		_enemyHexRerollFunc = enemyHexOptions?.RerollFunc;
		_enemyHexChanged = enemyHexOptions?.Changed;
		_playerRuneRerollLimit = HextechRuneConfiguration.ClampRerollLimit(playerRuneRerollLimit);
		_enemyHexRerollLimit = HextechRuneConfiguration.ClampRerollLimit(enemyHexOptions?.RerollLimit ?? HextechRuneConfiguration.GetDefaultMonsterHexRerollLimit());
		_titleOverride = titleOverride;
		_metadataMode = metadataMode;
		_enemyHexControlsEnabled = enemyHexOptions?.ControlsEnabled == true || enemyHexOptions?.RerollFunc != null;
		List<MonsterHexKind> initialMonsterHexes = enemyHexOptions?.InitialHexes?.ToList() ?? [];
		if (initialMonsterHexes.Count == 0 && enemyHexOptions?.InitialHex is { } initialHex)
		{
			initialMonsterHexes.Add(initialHex);
		}
		if (initialMonsterHexes.Count == 0 && monsterHexRelic != null && MonsterHexCatalog.TryGetMonsterHexKind(monsterHexRelic, out MonsterHexKind monsterHexKind))
		{
			initialMonsterHexes.Add(monsterHexKind);
		}
		foreach (MonsterHexKind monsterHex in initialMonsterHexes)
		{
			_monsterHexKinds.Add(monsterHex);
			_monsterHexBeforeRemoval.Add(null);
			_enemyHexRerollCounts.Add(0);
		}
		_rarityKey = DetermineRarityKey(relics, metadataMode);
		Name = nameof(HextechRuneSelectionScreen);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Enabled;
		Visible = true;
		BuildUi();
	}

	public static HextechRuneSelectionScreen Create(
		IReadOnlyList<RelicModel> relics,
		RelicModel? monsterHexRelic,
		Func<IReadOnlyList<RelicModel>, int, int, IReadOnlyList<RelicModel>>? rerollFunc = null,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null,
		int playerRuneRerollLimit = 1,
		string? titleOverride = null,
		HextechSelectionMetadataMode metadataMode = HextechSelectionMetadataMode.PlayerRune)
	{
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.Create: count={relics.Count}");
		return new HextechRuneSelectionScreen(relics, monsterHexRelic, rerollFunc, enemyHexOptions, playerRuneRerollLimit, titleOverride, metadataMode);
	}

	public override void _ExitTree()
	{
		EndMapPreview(restoreOverlay: false);
		RestoreMapButtonState();
		_mapPreviewHint?.QueueFree();
		_mapPreviewHint = null;
		// 场景重建、SL 或 overlay 被意外移除时不能把“尚未选择”伪装成空的成功结果。
		// 成功选择会先由 OnHolderSelected 完成 TCS；其余退出统一取消，让本幕保持未解析并可恢复。
		if (!_choiceLocked)
		{
			_completionSource.TrySetCanceled();
		}
		base._ExitTree();
	}

	private static RelicModel? CreateMonsterHexRelic(MonsterHexKind? monsterHex)
	{
		return monsterHex.HasValue
			? MonsterHexCatalog.GetIconRelicForMonsterHex(monsterHex.Value).ToMutable()
			: null;
	}
}
