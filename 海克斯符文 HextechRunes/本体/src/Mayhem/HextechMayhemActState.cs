using System.Text.Json;

namespace HextechRunes;

internal sealed class HextechMayhemActState
{
	private const int DefaultActCount = 3;
	private int[] _rarityByAct = NewUnknownArray();
	private List<MonsterHexKind>[] _monsterHexesByAct = NewMonsterHexLists();
	private int[] _resolvedActs = NewResolvedArray();
	private HashSet<int> _mapLengthReducedActs = new();
	private List<MonsterHexKind> _carriedMonsterHexes = new();
	private Dictionary<string, int> _extraStageIndexes = new(StringComparer.Ordinal);

	public int ActCount => _resolvedActs.Length;

	// 单调递增的脏标记:任何会改变 GetActiveMonsterHexes 结果的写入都自增它。
	// HextechActiveMonsterHexCache 比对这个版本号决定是否重算,取代了过去散落在
	// facade/SavedState 各处的手动 InvalidateActiveMonsterHexCache 调用。
	// 新增 mutating 方法时记得调用 MarkChanged()。
	public int Version { get; private set; }

	private void MarkChanged()
	{
		Version++;
	}

	public int[] SavedRarityByAct
	{
		get => _rarityByAct;
		set
		{
			_rarityByAct = NormalizeUnknownArray(value);
			EnsureCapacity(_rarityByAct.Length);
			MarkChanged();
		}
	}

	public int[] SavedMonsterHexByAct
	{
		get => _monsterHexesByAct
			.Select(static hexes => hexes.Count > 0 ? (int)hexes[0] : -1)
			.ToArray();
		set
		{
			MergeLegacyMonsterHexByAct(value);
			MarkChanged();
		}
	}

	public string SavedMonsterHexesByActJson
	{
		get => SerializeMonsterHexesByAct();
		set
		{
			RestoreMonsterHexesByAct(value);
			MarkChanged();
		}
	}

	public int[] SavedCarriedMonsterHexes
	{
		get => _carriedMonsterHexes.Select(static hex => (int)hex).ToArray();
		set
		{
			_carriedMonsterHexes = NormalizeMonsterHexList(value);
			MarkChanged();
		}
	}

	public int[] SavedResolvedActs
	{
		get => _resolvedActs;
		set
		{
			_resolvedActs = NormalizeResolvedArray(value);
			EnsureCapacity(_resolvedActs.Length);
			MarkChanged();
		}
	}

	public int[] SavedMapLengthReducedActs
	{
		get => _mapLengthReducedActs.OrderBy(static actIndex => actIndex).ToArray();
		set
		{
			_mapLengthReducedActs = NormalizeActIndexSet(value);
			MarkChanged();
		}
	}

	public string SavedExtraStageIndexesJson
	{
		get => SerializeExtraStageIndexes();
		set
		{
			RestoreExtraStageIndexes(value);
			MarkChanged();
		}
	}

	public bool IsResolved(int actIndex)
	{
		int slot = ToExistingActSlotOrInvalid(actIndex);
		return slot >= 0 && _resolvedActs[slot] > 0;
	}

	public void SetResolved(int actIndex, bool resolved)
	{
		int slot = EnsureActSlot(actIndex);
		if (slot >= 0)
		{
			_resolvedActs[slot] = resolved ? 1 : 0;
			MarkChanged();
		}
	}

	public bool TryMarkResolved(int actIndex)
	{
		int slot = EnsureActSlot(actIndex);
		if (slot < 0 || _resolvedActs[slot] > 0)
		{
			return false;
		}

		_resolvedActs[slot] = 1;
		MarkChanged();
		return true;
	}

	public bool IsMapLengthReduced(int actIndex)
	{
		return actIndex >= 0 && _mapLengthReducedActs.Contains(actIndex);
	}

	public void MarkMapLengthReduced(int actIndex)
	{
		if (actIndex >= 0 && _mapLengthReducedActs.Add(actIndex))
		{
			MarkChanged();
		}
	}

	public HextechRarityTier? GetRarity(int actIndex)
	{
		int slot = ToExistingActSlotOrInvalid(actIndex);
		if (slot < 0 || _rarityByAct[slot] < 0)
		{
			return null;
		}

		return (HextechRarityTier)_rarityByAct[slot];
	}

	public void SetRarity(int actIndex, HextechRarityTier rarity)
	{
		int slot = EnsureActSlot(actIndex);
		if (slot >= 0)
		{
			_rarityByAct[slot] = (int)rarity;
			MarkChanged();
		}
	}

	public bool TrySetRarityIfMissing(int actIndex, HextechRarityTier rarity)
	{
		int slot = EnsureActSlot(actIndex);
		if (slot < 0 || _rarityByAct[slot] >= 0)
		{
			return false;
		}

		_rarityByAct[slot] = (int)rarity;
		MarkChanged();
		return true;
	}

	public MonsterHexKind? GetMonsterHex(int actIndex)
	{
		IReadOnlyList<MonsterHexKind> hexes = GetMonsterHexes(actIndex);
		return hexes.Count > 0 ? hexes[0] : null;
	}

	public IReadOnlyList<MonsterHexKind> GetMonsterHexes(int actIndex)
	{
		int slot = ToExistingActSlotOrInvalid(actIndex);
		return slot >= 0 ? _monsterHexesByAct[slot].ToArray() : [];
	}

	public void SetMonsterHex(int actIndex, MonsterHexKind hex)
	{
		SetMonsterHexes(actIndex, [ hex ]);
	}

	public void SetMonsterHexes(int actIndex, IEnumerable<MonsterHexKind> hexes)
	{
		int slot = EnsureActSlot(actIndex);
		if (slot >= 0)
		{
			_monsterHexesByAct[slot] = NormalizeMonsterHexList(hexes.Select(static hex => (int)hex));
			MarkChanged();
		}
	}

	public void ClearMonsterHex(int actIndex)
	{
		int slot = ToExistingActSlotOrInvalid(actIndex);
		if (slot >= 0)
		{
			_monsterHexesByAct[slot].Clear();
			MarkChanged();
		}
	}

	public bool AddCarriedMonsterHex(MonsterHexKind hex)
	{
		if (_carriedMonsterHexes.Contains(hex))
		{
			return false;
		}

		_carriedMonsterHexes.Add(hex);
		MarkChanged();
		return true;
	}

	public bool RemoveMonsterHexEverywhere(MonsterHexKind hex)
	{
		bool removed = _carriedMonsterHexes.RemoveAll(existing => existing == hex) > 0;
		foreach (List<MonsterHexKind> hexes in _monsterHexesByAct)
		{
			removed |= hexes.RemoveAll(existing => existing == hex) > 0;
		}

		if (removed)
		{
			MarkChanged();
		}

		return removed;
	}

	public IReadOnlyList<MonsterHexKind> GetActiveMonsterHexes(int currentActIndex, Func<int, bool> shouldRecoverMonsterHex)
	{
		List<MonsterHexKind> result = new();
		HashSet<MonsterHexKind> seen = new();
		AddUnique(result, seen, _carriedMonsterHexes);

		int latestSlot = LatestActiveSlot(currentActIndex, shouldRecoverMonsterHex);
		if (latestSlot >= 0)
		{
			AddUnique(result, seen, _monsterHexesByAct[latestSlot]);
		}

		return result;
	}

	public IReadOnlyList<MonsterHexKind> GetActiveMonsterHexesBeforeAct(int actIndex)
	{
		List<MonsterHexKind> result = new();
		HashSet<MonsterHexKind> seen = new();
		AddUnique(result, seen, _carriedMonsterHexes);

		int previousSlot = Math.Min(actIndex - 1, _resolvedActs.Length - 1);
		for (int slot = previousSlot; slot >= 0; slot--)
		{
			if (_resolvedActs[slot] <= 0)
			{
				continue;
			}

			AddUnique(result, seen, _monsterHexesByAct[slot]);
			break;
		}

		return result;
	}

	public IReadOnlyList<MonsterHexKind> GetKnownMonsterHexes()
	{
		List<MonsterHexKind> result = new();
		HashSet<MonsterHexKind> seen = new();
		AddUnique(result, seen, _carriedMonsterHexes);
		foreach (List<MonsterHexKind> hexes in _monsterHexesByAct)
		{
			AddUnique(result, seen, hexes);
		}

		return result;
	}

	public IReadOnlyList<IReadOnlyList<MonsterHexKind>> GetMonsterHexRows()
	{
		List<IReadOnlyList<MonsterHexKind>> rows = new();
		HashSet<MonsterHexKind> seen = new();
		if (_carriedMonsterHexes.Count > 0)
		{
			List<MonsterHexKind> carried = new();
			AddUnique(carried, seen, _carriedMonsterHexes);
			if (carried.Count > 0)
			{
				rows.Add(carried);
			}
		}

		for (int stageIndex = 0; stageIndex < _monsterHexesByAct.Length; stageIndex++)
		{
			if (_resolvedActs[stageIndex] <= 0 && _monsterHexesByAct[stageIndex].Count == 0)
			{
				continue;
			}

			List<MonsterHexKind> delta = _monsterHexesByAct[stageIndex]
				.Where(hex => !seen.Contains(hex))
				.ToList();
			if (delta.Count > 0)
			{
				rows.Add(delta);
			}

			seen.Clear();
			foreach (MonsterHexKind hex in _carriedMonsterHexes)
			{
				seen.Add(hex);
			}
			foreach (MonsterHexKind hex in _monsterHexesByAct[stageIndex])
			{
				seen.Add(hex);
			}
		}

		return rows;
	}

	public int GetOrCreateExtraStageIndex(string stageKey, int minimumIndex)
	{
		if (string.IsNullOrWhiteSpace(stageKey))
		{
			throw new ArgumentException("Extra stage key cannot be empty.", nameof(stageKey));
		}

		if (_extraStageIndexes.TryGetValue(stageKey, out int existingIndex))
		{
			EnsureActSlot(existingIndex);
			return existingIndex;
		}

		int stageIndex = Math.Max(Math.Max(DefaultActCount, minimumIndex), _resolvedActs.Length);
		EnsureActSlot(stageIndex);
		_extraStageIndexes[stageKey] = stageIndex;
		MarkChanged();
		return stageIndex;
	}

	public int LastActIndexFor(int maxActIndex)
	{
		return maxActIndex;
	}

	public void Reset()
	{
		_rarityByAct = NewUnknownArray();
		_monsterHexesByAct = NewMonsterHexLists();
		_resolvedActs = NewResolvedArray();
		_mapLengthReducedActs.Clear();
		_carriedMonsterHexes.Clear();
		_extraStageIndexes.Clear();
		MarkChanged();
	}

	public void ResetForEndlessLoop()
	{
		// 无尽模式继续沿用单调递增的阶段序号，保留每次获得敌方海克斯的分组。
	}

	public void DebugSetOnlyMonsterHex(int actIndex, MonsterHexKind hex, HextechRarityTier rarity)
	{
		Reset();
		int slot = EnsureActSlot(actIndex);
		if (slot >= 0)
		{
			_rarityByAct[slot] = (int)rarity;
			_monsterHexesByAct[slot] = [ hex ];
			_resolvedActs[slot] = 1;
		}
	}

	public string Describe()
	{
		string monster = string.Join(";", _monsterHexesByAct.Select(static (hexes, index) => $"{index}=[{string.Join(",", hexes)}]"));
		return $"resolved={string.Join(",", _resolvedActs)} rarity={string.Join(",", _rarityByAct)} monster={monster} carried={string.Join(",", _carriedMonsterHexes)} mapReduced={string.Join(",", _mapLengthReducedActs.OrderBy(static actIndex => actIndex))}";
	}

	private void MergeLegacyMonsterHexByAct(int[]? value)
	{
		if (value == null || _monsterHexesByAct.Any(static hexes => hexes.Count > 0))
		{
			return;
		}

		List<MonsterHexKind> cumulative = new();
		HashSet<MonsterHexKind> seen = new();
		EnsureCapacity(value.Length);
		for (int actIndex = 0; actIndex < value.Length; actIndex++)
		{
			int rawHex = value[actIndex];
			if (Enum.IsDefined(typeof(MonsterHexKind), rawHex))
			{
				MonsterHexKind hex = (MonsterHexKind)rawHex;
				if (seen.Add(hex))
				{
					cumulative.Add(hex);
				}
			}

			_monsterHexesByAct[actIndex] = cumulative.ToList();
		}
	}

	private string SerializeMonsterHexesByAct()
	{
		int[][] raw = _monsterHexesByAct
			.Select(static hexes => hexes.Select(static hex => (int)hex).ToArray())
			.ToArray();
		return raw.Any(static hexes => hexes.Length > 0)
			? JsonSerializer.Serialize(raw)
			: "";
	}

	private void RestoreMonsterHexesByAct(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}

		try
		{
			int[][]? raw = JsonSerializer.Deserialize<int[][]>(json);
			if (raw == null)
			{
				return;
			}

			int normalizedLength = Math.Max(
				Math.Max(DefaultActCount, raw.Length),
				Math.Max(_rarityByAct.Length, _resolvedActs.Length));
			List<MonsterHexKind>[] normalized = NewMonsterHexLists(normalizedLength);
			for (int actIndex = 0; actIndex < raw.Length; actIndex++)
			{
				normalized[actIndex] = NormalizeMonsterHexList(raw[actIndex]);
			}

			_monsterHexesByAct = normalized;
			EnsureCapacity(normalizedLength);
		}
		catch (Exception ex)
		{
			_monsterHexesByAct = NewMonsterHexLists();
			string preview = json[..Math.Min(80, json.Length)];
			Log.Warn(
				$"[{ModInfo.Id}][Mayhem] Monster hexes by act restore failed; state cleared: "
				+ $"{ex.GetType().Name}: {ex.Message} json={preview}");
		}
	}

	private string SerializeExtraStageIndexes()
	{
		return _extraStageIndexes.Count == 0
			? ""
			: JsonSerializer.Serialize(new SortedDictionary<string, int>(_extraStageIndexes, StringComparer.Ordinal));
	}

	private void RestoreExtraStageIndexes(string? json)
	{
		_extraStageIndexes.Clear();
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}

		try
		{
			Dictionary<string, int>? restored = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
			if (restored == null)
			{
				return;
			}

			foreach ((string key, int stageIndex) in restored)
			{
				if (string.IsNullOrWhiteSpace(key) || stageIndex < DefaultActCount)
				{
					continue;
				}

				EnsureActSlot(stageIndex);
				_extraStageIndexes[key] = stageIndex;
			}
		}
		catch (Exception ex)
		{
			_extraStageIndexes.Clear();
			Log.Warn($"[{ModInfo.Id}][Mayhem] Extra stage index restore failed; mapping cleared: {ex.Message}");
		}
	}

	private int LatestActiveSlot(int maxSlot, Func<int, bool> shouldRecoverMonsterHex)
	{
		for (int slot = Math.Min(maxSlot, _resolvedActs.Length - 1); slot >= 0; slot--)
		{
			if (_resolvedActs[slot] > 0 || shouldRecoverMonsterHex(slot))
			{
				return slot;
			}
		}

		return -1;
	}

	private static void AddUnique(List<MonsterHexKind> result, HashSet<MonsterHexKind> seen, IEnumerable<MonsterHexKind> hexes)
	{
		foreach (MonsterHexKind hex in hexes)
		{
			if (seen.Add(hex))
			{
				result.Add(hex);
			}
		}
	}

	private int EnsureActSlot(int actIndex)
	{
		if (actIndex < 0)
		{
			return -1;
		}

		EnsureCapacity(actIndex + 1);
		return actIndex;
	}

	private int ToExistingActSlotOrInvalid(int actIndex)
	{
		return actIndex >= 0 && actIndex < _resolvedActs.Length ? actIndex : -1;
	}

	private void EnsureCapacity(int requiredLength)
	{
		int length = Math.Max(
			Math.Max(DefaultActCount, requiredLength),
			Math.Max(_rarityByAct.Length, Math.Max(_resolvedActs.Length, _monsterHexesByAct.Length)));
		if (_rarityByAct.Length < length)
		{
			int oldRarityLength = _rarityByAct.Length;
			Array.Resize(ref _rarityByAct, length);
			Array.Fill(_rarityByAct, -1, oldRarityLength, length - oldRarityLength);
		}

		if (_resolvedActs.Length < length)
		{
			Array.Resize(ref _resolvedActs, length);
		}

		if (_monsterHexesByAct.Length < length)
		{
			int oldMonsterLength = _monsterHexesByAct.Length;
			Array.Resize(ref _monsterHexesByAct, length);
			for (int i = oldMonsterLength; i < length; i++)
			{
				_monsterHexesByAct[i] = [];
			}
		}
	}

	private static int[] NewUnknownArray()
	{
		return [ -1, -1, -1 ];
	}

	private static int[] NewResolvedArray()
	{
		return [ 0, 0, 0 ];
	}

	private static List<MonsterHexKind>[] NewMonsterHexLists(int count = DefaultActCount)
	{
		return Enumerable.Range(0, count).Select(static _ => new List<MonsterHexKind>()).ToArray();
	}

	private static int[] NormalizeUnknownArray(int[]? value)
	{
		int[] normalized = Enumerable.Repeat(-1, Math.Max(DefaultActCount, value?.Length ?? 0)).ToArray();
		if (value == null)
		{
			return normalized;
		}

		for (int i = 0; i < value.Length; i++)
		{
			normalized[i] = value[i];
		}

		return normalized;
	}

	private static int[] NormalizeResolvedArray(int[]? value)
	{
		int[] normalized = new int[Math.Max(DefaultActCount, value?.Length ?? 0)];
		if (value == null)
		{
			return normalized;
		}

		for (int i = 0; i < value.Length; i++)
		{
			normalized[i] = value[i] > 0 ? 1 : 0;
		}

		return normalized;
	}

	private static List<MonsterHexKind> NormalizeMonsterHexList(IEnumerable<int>? value)
	{
		List<MonsterHexKind> normalized = new();
		HashSet<MonsterHexKind> seen = new();
		if (value == null)
		{
			return normalized;
		}

		foreach (int rawHex in value)
		{
			if (!Enum.IsDefined(typeof(MonsterHexKind), rawHex))
			{
				continue;
			}

			MonsterHexKind hex = (MonsterHexKind)rawHex;
			if (seen.Add(hex))
			{
				normalized.Add(hex);
			}
		}

		return normalized;
	}

	private static HashSet<int> NormalizeActIndexSet(int[]? value)
	{
		HashSet<int> normalized = new();
		if (value == null)
		{
			return normalized;
		}

		foreach (int actIndex in value)
		{
			if (actIndex >= 0)
			{
				normalized.Add(actIndex);
			}
		}

		return normalized;
	}
}
