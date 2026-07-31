namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	internal bool HasRuneSelectionJournalEntriesForAct(int actIndex)
	{
		return _runContext.RuneSelectionJournal.HasEntriesForAct(actIndex);
	}

	internal bool TryGetRuneSelectionJournalEntry(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId,
		out HextechRuneSelectionJournalEntry entry)
	{
		return _runContext.RuneSelectionJournal.TryGet(
			actIndex,
			choiceOrdinal,
			playerNetId,
			out entry);
	}

	internal bool TryRecoverRuneSelectionJournalEntryFromTelemetry(
		int actIndex,
		int choiceOrdinal,
		Player player,
		out HextechRuneSelectionJournalEntry entry)
	{
		entry = default;
		int playerSlot = -1;
		for (int i = 0; i < RunState.Players.Count; i++)
		{
			if (ReferenceEquals(RunState.Players[i], player))
			{
				playerSlot = i;
				break;
			}
		}

		if (playerSlot < 0)
		{
			return false;
		}

		HextechTelemetry.RuneChoiceRecord[] matchingRecords = _choiceHistory.GetTelemetryChoiceRecords()
			.Where(record =>
				record.ActIndex == actIndex
				&& record.ChoiceOrdinal == choiceOrdinal
				&& record.PlayerSlot == playerSlot
				&& !string.IsNullOrWhiteSpace(record.Selected))
			.Take(2)
			.ToArray();
		if (matchingRecords.Length != 1)
		{
			return false;
		}

		HextechTelemetry.RuneChoiceRecord matchingRecord = matchingRecords[0];
		string selectedEntry = matchingRecord.Selected!;
		if (!matchingRecord.Options.Contains(selectedEntry, StringComparer.Ordinal)
			|| !Enum.TryParse(
				matchingRecord.Rarity,
				ignoreCase: false,
				out HextechRarityTier recordedRarity)
			|| _actState.GetRarity(actIndex) != recordedRarity)
		{
			return false;
		}

		ModelId? selectedId = HextechCatalog.GetConfigurablePlayerRuneIds()
			.SingleOrDefault(id => string.Equals(id.Entry, selectedEntry, StringComparison.Ordinal));
		if (selectedId == null)
		{
			return false;
		}

		_runContext.RuneSelectionJournal.RecordSelected(
			actIndex,
			choiceOrdinal,
			player.NetId,
			selectedId);
		entry = new HextechRuneSelectionJournalEntry(selectedId, Applied: false);
		return true;
	}

	internal bool RecordRuneSelectionJournalSelection(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId,
		ModelId selectedId)
	{
		return _runContext.RuneSelectionJournal.RecordSelected(
			actIndex,
			choiceOrdinal,
			playerNetId,
			selectedId);
	}

	internal bool MarkRuneSelectionJournalApplied(
		int actIndex,
		int choiceOrdinal,
		ulong playerNetId,
		ModelId selectedId)
	{
		return _runContext.RuneSelectionJournal.MarkApplied(
			actIndex,
			choiceOrdinal,
			playerNetId,
			selectedId);
	}
}
