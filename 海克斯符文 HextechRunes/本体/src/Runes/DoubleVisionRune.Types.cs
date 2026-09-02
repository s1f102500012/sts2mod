using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace HextechRunes;

public sealed partial class DoubleVisionRune
{
	private sealed class CardRewardTracker
	{
		public CardRewardTracker(Player player)
		{
			Player = player;
		}

		public Player Player { get; }

		public List<CardModel> AddedCards { get; } = [];
	}

	private class RewardDuplicationScope
	{
		public RewardDuplicationScope(IReadOnlyList<DoubleVisionRune> runes)
		{
			Runes = runes;
		}

		public IReadOnlyList<DoubleVisionRune> Runes { get; }
	}

	private sealed class DirectCommandRewardScope : RewardDuplicationScope
	{
		public DirectCommandRewardScope(Player player, IReadOnlyList<DoubleVisionRune> runes, int previousSuppressionDepth)
			: base(runes)
		{
			Player = player;
			PreviousSuppressionDepth = previousSuppressionDepth;
		}

		public Player Player { get; }

		public int PreviousSuppressionDepth { get; }

		public decimal GoldAmount { get; set; }

		public bool WasGoldStolenBack { get; set; }
	}

	private sealed class EventRelicTransaction
	{
		private readonly EventRewardTransaction<EventRelicIntent> _transaction = new();

		public EventRelicTransaction(
			RunState runState,
			EventRoom eventRoom,
			EventRelicTransactionBatch batch)
		{
			RunState = runState;
			EventRoom = eventRoom;
			Batch = batch;
		}

		public RunState RunState { get; }

		public EventRoom EventRoom { get; }

		public EventRelicTransactionBatch Batch { get; }

		public int Count => _transaction.Count;

		public bool IsCommitting { get; private set; }

		public bool IsAcceptingRecords => _transaction.IsAcceptingRecords;

		public void Record(EventRelicIntent intent)
		{
			_transaction.Record(intent);
		}

		public bool TryRecord(EventRelicIntent intent)
		{
			return _transaction.TryRecord(intent);
		}

		public void CloseForRecording()
		{
			_transaction.CloseForRecording();
		}

		public async Task CommitSequentially(Func<EventRelicIntent, Task> commit)
		{
			IsCommitting = true;
			try
			{
				await _transaction.CommitSequentially(commit);
			}
			finally
			{
				IsCommitting = false;
			}
		}
	}

	private sealed class EventRelicTransactionBatch
	{
		private readonly object _lock = new();
		private int _activeTransactions;
		private bool _hasCommittedRewardsSinceLastSave;

		public void Begin()
		{
			lock (_lock)
			{
				_activeTransactions++;
			}
		}

		public bool Complete(bool committedRewards, bool canSaveFinishedAncientEvent)
		{
			lock (_lock)
			{
				if (_activeTransactions <= 0)
				{
					throw new InvalidOperationException("Event relic transaction batch completed without a matching begin.");
				}

				_activeTransactions--;
				_hasCommittedRewardsSinceLastSave |= committedRewards;
				if (_activeTransactions != 0
					|| !_hasCommittedRewardsSinceLastSave
					|| !canSaveFinishedAncientEvent)
				{
					return false;
				}

				_hasCommittedRewardsSinceLastSave = false;
				return true;
			}
		}
	}

	private sealed record EventRelicIntent(
		Player Player,
		RelicModel ObtainedRelic,
		IReadOnlyList<DoubleVisionRune> Runes);

	private sealed record EventRelicTransactionScope(
		EventRelicTransaction Transaction,
		EventRelicTransaction? Previous);

	private sealed record EventRelicRecordScope(
		EventRelicTransaction Transaction,
		Player Player,
		RelicModel AttemptedRelic,
		IReadOnlyList<DoubleVisionRune> Runes,
		int PreviousObtainDepth,
		bool IsOutermostObtain);

	private sealed class CardRewardTrackingScope : RewardDuplicationScope
	{
		public CardRewardTrackingScope(Player player, IReadOnlyList<DoubleVisionRune> runes, CardRewardTracker? previousTracker)
			: base(runes)
		{
			Tracker = new CardRewardTracker(player);
			PreviousTracker = previousTracker;
		}

		public CardRewardTracker Tracker { get; }

		public CardRewardTracker? PreviousTracker { get; }
	}
}
