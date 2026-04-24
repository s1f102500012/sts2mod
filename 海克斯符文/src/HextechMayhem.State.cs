using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private static readonly IReadOnlyList<int> DefaultArray = [ -1, -1, -1 ];

	private int[] _rarityByAct = [ -1, -1, -1 ];
	private int[] _monsterHexByAct = [ -1, -1, -1 ];
	private int[] _resolvedActs = [ 0, 0, 0 ];

	private readonly Dictionary<uint, int> _slapProcsThisTurn = new();
	private readonly Dictionary<uint, int> _tormentorProcsThisTurn = new();
	private readonly Dictionary<uint, int> _courageProcsThisTurn = new();
	private readonly HashSet<uint> _escapePlanTriggered = new();
	private readonly HashSet<uint> _escapePlanPending = new();
	private readonly HashSet<uint> _repulsorTriggered = new();
	private readonly HashSet<uint> _repulsorPending = new();
	private readonly HashSet<uint> _dawnTriggered = new();
	private readonly HashSet<uint> _goliathApplied = new();
	private readonly HashSet<uint> _bigStrengthApplied = new();
	private readonly HashSet<uint> _protectiveVeilApplied = new();
	private readonly HashSet<uint> _thornmailApplied = new();
	private readonly HashSet<uint> _superBrainApplied = new();
	private readonly HashSet<uint> _astralBodyApplied = new();
	private readonly HashSet<uint> _drawYourSwordApplied = new();
	private readonly HashSet<uint> _madScientistApplied = new();
	private readonly Dictionary<uint, int> _tankEngineStacks = new();
	private readonly Dictionary<uint, int> _shrinkEngineStacks = new();
	private readonly Dictionary<uint, int> _getExcitedPending = new();
		private readonly HashSet<uint> _feelTheBurnPending = new();
		private readonly HashSet<uint> _mountainSoulHasPreviousTurn = new();
		private readonly HashSet<uint> _mountainSoulDamagedSinceLastTurn = new();
		private readonly Dictionary<ulong, int> _playerAttackCardsPlayedThisCombat = new();
		private readonly HashSet<string> _monsterDebuffActionProcKeysThisTurn = new();
		private readonly HashSet<string> _groupedPlayerDebuffProcKeys = new();
		private string? _lastEnemyThresholdTriggerKey;
		private bool _handlingMonsterTormentorBurn;
		private bool _handlingMonsterBuffer;
		private bool _handlingServantMasterIllusion;
		private bool _handlingGroupedPlayerDebuffs;
		private int _enemyProtectiveVeilTurnCounter;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedRarityByAct
	{
		get => _rarityByAct;
		set => _rarityByAct = NormalizeSavedArray(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedMonsterHexByAct
	{
		get => _monsterHexByAct;
		set => _monsterHexByAct = NormalizeSavedArray(value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int[] SavedResolvedActs
	{
		get => _resolvedActs;
		set => _resolvedActs = NormalizeResolvedArray(value);
	}

	public override LocString Title => new("modifiers", "HEXTECH_MAYHEM.title");

	public override LocString Description => new("modifiers", "HEXTECH_MAYHEM.description");

	protected override string IconPath => ImageHelper.GetImagePath("powers/missing_power.png");

	public override IEnumerable<IHoverTip> HoverTips => [];

	public RunState ActiveRunState => RunState;

	public bool IsActResolved(int actIndex)
	{
		return actIndex >= 0 && actIndex < _resolvedActs.Length && _resolvedActs[actIndex] > 0;
	}

	public void SetActResolved(int actIndex, bool resolved)
	{
		if (actIndex >= 0 && actIndex < _resolvedActs.Length)
		{
			_resolvedActs[actIndex] = resolved ? 1 : 0;
		}
	}

	public HextechRarityTier? GetRarityForAct(int actIndex)
	{
		if (actIndex < 0 || actIndex >= _rarityByAct.Length || _rarityByAct[actIndex] < 0)
		{
			return null;
		}

		return (HextechRarityTier)_rarityByAct[actIndex];
	}

	public void SetRarityForAct(int actIndex, HextechRarityTier rarity)
	{
		if (actIndex >= 0 && actIndex < _rarityByAct.Length)
		{
			_rarityByAct[actIndex] = (int)rarity;
		}
	}

	public MonsterHexKind? GetMonsterHexForAct(int actIndex)
	{
		if (actIndex < 0 || actIndex >= _monsterHexByAct.Length || _monsterHexByAct[actIndex] < 0)
		{
			return null;
		}

		return (MonsterHexKind)_monsterHexByAct[actIndex];
	}

	public void SetMonsterHexForAct(int actIndex, MonsterHexKind hex)
	{
		if (actIndex >= 0 && actIndex < _monsterHexByAct.Length)
		{
			_monsterHexByAct[actIndex] = (int)hex;
		}
	}

	public IReadOnlyList<MonsterHexKind> GetActiveMonsterHexes()
	{
		List<MonsterHexKind> result = new();
		HashSet<MonsterHexKind> seen = new();
		for (int actIndex = 0; actIndex <= RunState.CurrentActIndex && actIndex < _monsterHexByAct.Length; actIndex++)
		{
			if (IsActResolved(actIndex) && _monsterHexByAct[actIndex] >= 0)
			{
				MonsterHexKind hex = (MonsterHexKind)_monsterHexByAct[actIndex];
				if (seen.Add(hex))
				{
					result.Add(hex);
				}
			}
		}

		return result;
	}

	public void ResetForNewRun()
	{
		_rarityByAct = [ -1, -1, -1 ];
		_monsterHexByAct = [ -1, -1, -1 ];
		_resolvedActs = [ 0, 0, 0 ];
		ResetCombatTracking();
	}

	public void DebugSetOnlyMonsterHex(int actIndex, MonsterHexKind hex, HextechRarityTier rarity)
	{
		_rarityByAct = [ -1, -1, -1 ];
		_monsterHexByAct = [ -1, -1, -1 ];
		_resolvedActs = [ 0, 0, 0 ];
		if (actIndex >= 0 && actIndex < _monsterHexByAct.Length)
		{
			_rarityByAct[actIndex] = (int)rarity;
			_monsterHexByAct[actIndex] = (int)hex;
			_resolvedActs[actIndex] = 1;
		}

		ResetCombatTracking();
	}

	public bool HasActiveMonsterHex(MonsterHexKind hex)
	{
		return GetActiveMonsterHexes().Contains(hex);
	}

	private static int[] NormalizeSavedArray(int[]? value)
	{
		int[] normalized = DefaultArray.ToArray();
		if (value == null)
		{
			return normalized;
		}

		for (int i = 0; i < Math.Min(normalized.Length, value.Length); i++)
		{
			normalized[i] = value[i];
		}

		return normalized;
	}

	private static int[] NormalizeResolvedArray(int[]? value)
	{
		int[] normalized = [ 0, 0, 0 ];
		if (value == null)
		{
			return normalized;
		}

		for (int i = 0; i < Math.Min(normalized.Length, value.Length); i++)
		{
			normalized[i] = value[i] > 0 ? 1 : 0;
		}

		return normalized;
	}

	private void ResetCombatTracking()
	{
		_slapProcsThisTurn.Clear();
		_tormentorProcsThisTurn.Clear();
		_courageProcsThisTurn.Clear();
		_escapePlanTriggered.Clear();
		_escapePlanPending.Clear();
		_repulsorTriggered.Clear();
		_repulsorPending.Clear();
		_dawnTriggered.Clear();
		_goliathApplied.Clear();
		_bigStrengthApplied.Clear();
		_protectiveVeilApplied.Clear();
		_thornmailApplied.Clear();
		_superBrainApplied.Clear();
		_astralBodyApplied.Clear();
		_drawYourSwordApplied.Clear();
		_madScientistApplied.Clear();
		_tankEngineStacks.Clear();
			_shrinkEngineStacks.Clear();
			_getExcitedPending.Clear();
			_feelTheBurnPending.Clear();
			_mountainSoulHasPreviousTurn.Clear();
			_mountainSoulDamagedSinceLastTurn.Clear();
			_playerAttackCardsPlayedThisCombat.Clear();
			_monsterDebuffActionProcKeysThisTurn.Clear();
			_groupedPlayerDebuffProcKeys.Clear();
			_lastEnemyThresholdTriggerKey = null;
			_enemyProtectiveVeilTurnCounter = 0;
			_handlingMonsterBuffer = false;
			_handlingServantMasterIllusion = false;
			_handlingGroupedPlayerDebuffs = false;
		}
	}
