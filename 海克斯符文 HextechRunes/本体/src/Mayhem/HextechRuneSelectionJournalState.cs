using System.Text.Json;

namespace HextechRunes;

internal readonly record struct HextechRuneSelectionJournalEntry(
	ModelId SelectedId,
	// Applied 以遗物已进入背包为提交边界；AfterObtained 若在插入后失败，不可重跑 Obtain，
	// 否则会重复遗物及已执行的拾取副作用。该异常必须中止联机事务并保留诊断。
	bool Applied);

internal sealed class HextechRuneSelectionJournalState
{
	private const int CurrentVersion = 1;

	private readonly object _syncRoot = new();
	private readonly Dictionary<JournalKey, HextechRuneSelectionJournalEntry> _entries = new();

	internal static bool RequiresRelicObtain(bool applied, bool currentlyOwned)
	{
		return !applied && !currentlyOwned;
	}

	public bool TryGet(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId,
		out HextechRuneSelectionJournalEntry entry)
	{
		JournalKey key = CreateKey(actIndex, choiceOrdinal, playerNetId);
		lock (_syncRoot)
		{
			return _entries.TryGetValue(key, out entry);
		}
	}

	public bool HasEntriesForAct(int actIndex)
	{
		if (actIndex < 0)
		{
			return false;
		}

		lock (_syncRoot)
		{
			return _entries.Keys.Any(key => key.ActIndex == actIndex);
		}
	}

	public bool RecordSelected(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId,
		ModelId selectedId)
	{
		JournalKey key = CreateKey(actIndex, choiceOrdinal, playerNetId);
		ValidateModelId(selectedId);

		lock (_syncRoot)
		{
			if (_entries.TryGetValue(key, out HextechRuneSelectionJournalEntry existing))
			{
				if (!HasSameModelId(existing.SelectedId, selectedId))
				{
					throw new InvalidOperationException(
						$"[{ModInfo.Id}][Mayhem] Rune selection journal conflict: "
						+ $"act={actIndex} ordinal={choiceOrdinal} player={playerNetId} "
						+ $"existing={Describe(existing.SelectedId)} incoming={Describe(selectedId)}.");
				}

				return false;
			}

			_entries.Add(key, new HextechRuneSelectionJournalEntry(selectedId, Applied: false));
			return true;
		}
	}

	public bool MarkApplied(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId,
		ModelId selectedId)
	{
		JournalKey key = CreateKey(actIndex, choiceOrdinal, playerNetId);
		ValidateModelId(selectedId);

		lock (_syncRoot)
		{
			if (!_entries.TryGetValue(key, out HextechRuneSelectionJournalEntry existing))
			{
				throw new InvalidOperationException(
					$"[{ModInfo.Id}][Mayhem] Rune selection journal cannot mark an unrecorded selection as applied: "
					+ $"act={actIndex} ordinal={choiceOrdinal} player={playerNetId} selected={Describe(selectedId)}.");
			}

			if (!HasSameModelId(existing.SelectedId, selectedId))
			{
				throw new InvalidOperationException(
					$"[{ModInfo.Id}][Mayhem] Rune selection journal apply mismatch: "
					+ $"act={actIndex} ordinal={choiceOrdinal} player={playerNetId} "
					+ $"recorded={Describe(existing.SelectedId)} applied={Describe(selectedId)}.");
			}

			if (existing.Applied)
			{
				return false;
			}

			_entries[key] = existing with { Applied = true };
			return true;
		}
	}

	public string Serialize()
	{
		lock (_syncRoot)
		{
			if (_entries.Count == 0)
			{
				return "";
			}

			JournalJsonEntry[] entries = _entries
				.OrderBy(static pair => pair.Key.ActIndex)
				.ThenBy(static pair => pair.Key.ChoiceOrdinal)
				.ThenBy(static pair => pair.Key.PlayerNetId)
				.Select(static pair => new JournalJsonEntry(
					pair.Key.ActIndex,
					pair.Key.ChoiceOrdinal,
					pair.Key.PlayerNetId,
					pair.Value.SelectedId.Category,
					pair.Value.SelectedId.Entry,
					pair.Value.Applied))
				.ToArray();
			return JsonSerializer.Serialize(
				new JournalJsonSnapshot(CurrentVersion, entries),
				HextechTelemetry.JsonOptions);
		}
	}

	public void Restore(string? json)
	{
		lock (_syncRoot)
		{
			_entries.Clear();
			if (string.IsNullOrWhiteSpace(json))
			{
				return;
			}

			JournalJsonSnapshot? snapshot;
			try
			{
				snapshot = JsonSerializer.Deserialize<JournalJsonSnapshot>(
					json,
					HextechTelemetry.JsonOptions);
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Rune selection journal restore failed; journal cleared: {ex.Message}");
				return;
			}

			if (snapshot == null || snapshot.Version != CurrentVersion || snapshot.Entries == null)
			{
				Log.Warn(
					$"[{ModInfo.Id}][Mayhem] Rune selection journal restore ignored unsupported payload: "
					+ $"version={snapshot?.Version.ToString() ?? "null"}.");
				return;
			}

			int ignored = 0;
			HashSet<JournalKey> conflictedKeys = [];
			foreach (JournalJsonEntry? serialized in snapshot.Entries)
			{
				if (!TryRestoreEntry(serialized, conflictedKeys))
				{
					ignored++;
				}
			}

			if (ignored > 0)
			{
				Log.Warn(
					$"[{ModInfo.Id}][Mayhem] Rune selection journal restore ignored invalid or conflicting entries: "
					+ $"ignored={ignored} restored={_entries.Count}.");
			}
		}
	}

	public void Reset()
	{
		lock (_syncRoot)
		{
			_entries.Clear();
		}
	}

	private bool TryRestoreEntry(
		JournalJsonEntry? serialized,
		HashSet<JournalKey> conflictedKeys)
	{
		if (serialized == null
			|| serialized.ActIndex < 0
			|| serialized.ChoiceOrdinal < 0
			|| string.IsNullOrWhiteSpace(serialized.Category)
			|| string.IsNullOrWhiteSpace(serialized.Entry))
		{
			return false;
		}

		JournalKey key = new(
			serialized.ActIndex,
			serialized.ChoiceOrdinal,
			serialized.PlayerNetId);
		if (conflictedKeys.Contains(key))
		{
			return false;
		}

		ModelId selectedId;
		try
		{
			selectedId = new ModelId(serialized.Category, serialized.Entry);
		}
		catch (Exception)
		{
			return false;
		}

		HextechRuneSelectionJournalEntry restored = new(selectedId, serialized.Applied);
		if (!_entries.TryGetValue(key, out HextechRuneSelectionJournalEntry existing))
		{
			_entries.Add(key, restored);
			return true;
		}

		if (HasSameModelId(existing.SelectedId, selectedId))
		{
			_entries[key] = existing with { Applied = existing.Applied || restored.Applied };
			return true;
		}

		// 同一 operation 对应两个不同模型时无法安全推断哪一个已发放，删除该键让上层重新走同步选择。
		_entries.Remove(key);
		conflictedKeys.Add(key);
		return false;
	}

	private static JournalKey CreateKey(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId)
	{
		if (actIndex < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(actIndex), actIndex, "Act index must be non-negative.");
		}
		if (choiceOrdinal < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(choiceOrdinal), choiceOrdinal, "Choice ordinal must be non-negative.");
		}

		return new JournalKey(actIndex, choiceOrdinal, playerNetId);
	}

	private static void ValidateModelId(ModelId id)
	{
		if (string.IsNullOrWhiteSpace(id.Category) || string.IsNullOrWhiteSpace(id.Entry))
		{
			throw new ArgumentException("Rune selection journal ModelId must include both category and entry.", nameof(id));
		}
	}

	private static bool HasSameModelId(ModelId left, ModelId right)
	{
		return string.Equals(left.Category, right.Category, StringComparison.Ordinal)
			&& string.Equals(left.Entry, right.Entry, StringComparison.Ordinal);
	}

	private static string Describe(ModelId id)
	{
		return $"{id.Category}:{id.Entry}";
	}

	private readonly record struct JournalKey(
		int ActIndex,
		int ChoiceOrdinal,
		ulong PlayerNetId);

	private sealed record JournalJsonSnapshot(
		int Version,
		JournalJsonEntry?[]? Entries);

	private sealed record JournalJsonEntry(
		int ActIndex,
		int ChoiceOrdinal,
		ulong PlayerNetId,
		string Category,
		string Entry,
		bool Applied);
}
