using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace HextechRunes;

public sealed partial class DoubleVisionRune
{
	internal static object? BeginCardRewardTracking(Player player)
	{
		IReadOnlyList<DoubleVisionRune> runes = GetActiveRunes(player);
		if (runes.Count == 0)
		{
			return null;
		}

		CardRewardTracker? previousTracker = CurrentCardRewardTracker.Value;
		CardRewardTrackingScope scope = new(player, runes, previousTracker);
		CurrentCardRewardTracker.Value = scope.Tracker;
		return scope;
	}

	internal static Task<bool> CompleteCardRewardAsync(Task<bool> originalTask, object? trackingState)
	{
		if (trackingState is not CardRewardTrackingScope scope)
		{
			return originalTask;
		}

		CurrentCardRewardTracker.Value = scope.PreviousTracker;
		return CompleteCardRewardAddTrackingAsync(originalTask, scope);
	}

	internal static object? CaptureRewardDuplicationState(Player player)
	{
		IReadOnlyList<DoubleVisionRune> runes = GetActiveRunes(player);
		return runes.Count == 0 ? null : new RewardDuplicationScope(runes);
	}

	internal static object? BeginRewardCommandSuppression()
	{
		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		return previousDepth;
	}

	internal static void CompleteRewardCommandSuppression(object? suppressionState)
	{
		if (suppressionState is int previousDepth)
		{
			CommandDuplicationSuppressionDepth.Value = previousDepth;
		}
	}

	internal static object? BeginDirectRelicReward(RelicModel relic, Player player)
	{
		return BeginEventRelicRecording(relic, player) ?? (object?)BeginDirectCommandReward(player);
	}

	internal static Task<RelicModel> CompleteDirectRelicRewardAsync(Task<RelicModel> originalTask, object? duplicationState)
	{
		if (duplicationState is EventRelicRecordScope recordScope)
		{
			EventRelicObtainDepth.Value = recordScope.PreviousObtainDepth;
			return RecordEventRelicAsync(originalTask, recordScope);
		}

		if (duplicationState is not DirectCommandRewardScope scope)
		{
			return originalTask;
		}

		RestoreCommandRewardScope(scope);
		return CompleteDirectRelicRewardAsync(originalTask, scope);
	}

	private static EventRelicRecordScope? BeginEventRelicRecording(RelicModel relic, Player player)
	{
		EventRelicTransaction? transaction = CurrentEventRelicTransaction.Value;
		if (transaction == null
			|| !transaction.IsAcceptingRecords
			|| transaction.IsCommitting
			|| IsCommandDuplicationSuppressed()
			|| CombatManager.Instance.IsInProgress
			|| player.RunState.CurrentRoom is not EventRoom
			|| player.Creature.IsDead)
		{
			return null;
		}

		IReadOnlyList<DoubleVisionRune> runes = GetEventActiveRunes(player);
		if (runes.Count == 0)
		{
			return null;
		}

		int previousDepth = EventRelicObtainDepth.Value;
		EventRelicObtainDepth.Value = previousDepth + 1;
		return new EventRelicRecordScope(transaction, player, relic, runes, previousDepth, previousDepth == 0);
	}

	private static async Task<RelicModel> RecordEventRelicAsync(Task<RelicModel> originalTask, EventRelicRecordScope scope)
	{
		RelicModel obtained;
		try
		{
			obtained = await originalTask;
		}
		catch (Exception originalException) when (
			originalException is not OperationCanceledException
			&& IsCustomRelic(scope.AttemptedRelic))
		{
			RelicModel? recovered = await TryRecoverExternalEventRelicObtain(scope, originalException);
			if (recovered == null)
			{
				throw;
			}

			Log.Warn(
				$"[{ModInfo.Id}][DoubleVision] Skipped duplication after recovering a failed custom event relic obtain: "
				+ $"player={scope.Player.NetId} relic={(recovered.CanonicalInstance?.Id ?? recovered.Id).Entry}.");
			return recovered;
		}

		if (!scope.IsOutermostObtain
			|| obtained.Owner == null
			|| obtained.Owner.Creature.IsDead
			|| !ReferenceEquals(obtained.Owner, scope.Player))
		{
			return obtained;
		}

		if (!scope.Transaction.TryRecord(new EventRelicIntent(scope.Player, obtained, scope.Runes)))
		{
			Log.Warn(
				$"[{ModInfo.Id}][DoubleVision] Skipped late event relic duplication after its option transaction closed: "
				+ $"player={scope.Player.NetId} relic={(obtained.CanonicalInstance?.Id ?? obtained.Id).Entry}.");
		}

		return obtained;
	}

	private static bool IsCustomRelic(RelicModel relic)
	{
		return relic.GetType().Assembly != typeof(RelicModel).Assembly;
	}

	private static async Task<RelicModel?> TryRecoverExternalEventRelicObtain(
		EventRelicRecordScope scope,
		Exception originalException)
	{
		RelicModel relic = scope.AttemptedRelic;
		Player player = scope.Player;
		ModelId relicId = relic.CanonicalInstance?.Id ?? relic.Id;
		Log.Warn(
			$"[{ModInfo.Id}][DoubleVision] Custom event relic obtain failed; attempting a history-independent fallback: "
			+ $"player={player.NetId} relic={relicId.Entry} type={relic.GetType().FullName} "
			+ $"error={originalException.GetType().Name}: {originalException.Message}");

		return await TryKeepRelicWithoutHistory(
			player,
			relic,
			relicId,
			runAfterObtained: true,
			context: "Custom event relic fallback",
			failureIsDesyncRisk: false);
	}

	private static async Task<RelicModel?> TryKeepRelicWithoutHistory(
		Player player,
		RelicModel relic,
		ModelId relicId,
		bool runAfterObtained,
		string context,
		bool failureIsDesyncRisk)
	{
		try
		{
			bool addedByFallback = !player.Relics.Contains(relic);
			if (addedByFallback)
			{
				relic.AssertMutable();
				player.AddRelicInternal(relic);
			}

			if (!relic.IsStackable)
			{
				player.RelicGrabBag.Remove(relic);
				player.RunState.SharedRelicGrabBag.Remove(relic);
			}

			relic.FloorAddedToDeck = player.RunState.TotalFloor;
			if (addedByFallback && runAfterObtained)
			{
				try
				{
					await relic.AfterObtained();
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception afterObtainedException)
				{
					string message =
						$"[{ModInfo.Id}][DoubleVision]{(failureIsDesyncRisk ? "[DESYNC-RISK]" : "")} "
						+ $"{context} kept the relic but its pickup effect failed: "
						+ $"player={player.NetId} relic={relicId.Entry} "
						+ $"error={afterObtainedException.GetType().Name}: {afterObtainedException.Message}";
					if (failureIsDesyncRisk)
					{
						Log.Error(message);
					}
					else
					{
						Log.Warn(message);
					}
				}
			}

			Log.Warn(
				$"[{ModInfo.Id}][DoubleVision] {context} completed without duplicating run-history writes: "
				+ $"player={player.NetId} relic={relicId.Entry}.");
			return relic;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception recoveryException)
		{
			string message =
				$"[{ModInfo.Id}][DoubleVision]{(failureIsDesyncRisk ? "[DESYNC-RISK]" : "")} {context} failed: "
				+ $"player={player.NetId} relic={relicId.Entry} "
				+ $"error={recoveryException.GetType().Name}: {recoveryException.Message}";
			if (failureIsDesyncRisk)
			{
				Log.Error(message);
			}
			else
			{
				Log.Warn(message);
			}

			return null;
		}
	}

	internal static object? BeginEventOptionRelicTransaction(EventOption option)
	{
		if (option.IsProceed
			|| RunManager.Instance.DebugOnlyGetState() is not RunState runState
			|| runState.CurrentRoom is not EventRoom eventRoom
			|| !runState.Players.Any(static player => GetEventActiveRunes(player).Count > 0))
		{
			return null;
		}

		EventRelicTransaction? previous = CurrentEventRelicTransaction.Value;
		EventRelicTransactionBatch batch = EventRelicTransactionBatches.GetValue(
			eventRoom,
			static _ => new EventRelicTransactionBatch());
		batch.Begin();
		EventRelicTransaction transaction = new(runState, eventRoom, batch);
		CurrentEventRelicTransaction.Value = transaction;
		return new EventRelicTransactionScope(transaction, previous);
	}

	internal static Task CompleteEventOptionRelicTransactionAsync(Task originalTask, object? transactionState)
	{
		if (transactionState is not EventRelicTransactionScope scope)
		{
			return originalTask;
		}

		CurrentEventRelicTransaction.Value = scope.Previous;
		return CompleteEventOptionRelicTransactionAsync(originalTask, scope.Transaction);
	}

	private static async Task CompleteEventOptionRelicTransactionAsync(Task originalTask, EventRelicTransaction transaction)
	{
		bool committedRewards = false;
		bool shouldSave = false;
		try
		{
			await originalTask;
			transaction.CloseForRecording();
			await transaction.CommitSequentially(CommitEventRelicIntent);
			committedRewards = transaction.Count > 0;
		}
		finally
		{
			transaction.CloseForRecording();
			bool canSaveFinishedAncientEvent = ReferenceEquals(
					RunManager.Instance.DebugOnlyGetState(),
					transaction.RunState)
				&& ReferenceEquals(transaction.RunState.CurrentRoom, transaction.EventRoom)
				&& transaction.EventRoom.IsPreFinished
				&& RunManager.Instance.EventSynchronizer.Events.All(static eventModel => eventModel.IsFinished);
			shouldSave = transaction.Batch.Complete(committedRewards, canSaveFinishedAncientEvent);
		}

		// AncientEvent 完成时原版会立即 fire-and-forget 一次存档,它可能早于本外层事务提交。
		// 同一事件房最后一个活跃选项事务负责补写一次完整状态,避免共享事件保存到半提交快照。
		if (shouldSave)
		{
			await SaveManager.Instance.SaveRun(transaction.EventRoom);
		}
	}

	private static async Task CommitEventRelicIntent(EventRelicIntent intent)
	{
		Player player = intent.Player;
		RelicModel sourceRelic = intent.ObtainedRelic;
		if (player.Creature.IsDead
			|| !ReferenceEquals(sourceRelic.Owner, player)
			|| !player.Relics.Contains(sourceRelic))
		{
			return;
		}

		foreach (DoubleVisionRune rune in intent.Runes)
		{
			HashSet<RelicModel> relicsBefore = new(player.Relics, ReferenceEqualityComparer.Instance);
			try
			{
				await rune.DuplicateObtainedRelic(player, sourceRelic, syncReward: false);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception exception)
			{
				ModelId sourceId = sourceRelic.CanonicalInstance?.Id ?? sourceRelic.Id;
				RelicModel? recoveryCopy = player.Relics.FirstOrDefault(
					relic => !relicsBefore.Contains(relic)
						&& (relic.CanonicalInstance?.Id ?? relic.Id) == sourceId);
				Exception? recoveryCopyException = null;
				if (recoveryCopy == null)
				{
					recoveryCopy = TryCreateEventRelicRecoveryCopy(sourceRelic, sourceId, out recoveryCopyException);
				}

				if (recoveryCopy == null)
				{
					string recoveryFailure = recoveryCopyException == null
						? "canonical recovery model unavailable"
						: $"{recoveryCopyException.GetType().Name}: {recoveryCopyException.Message}";
					Log.Error(
						$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Event relic copy failed and no deterministic recovery copy could be created: "
						+ $"player={player.NetId} relic={sourceId.Entry} "
						+ $"error={exception.GetType().Name}: {exception.Message} recoveryError={recoveryFailure}");
					continue;
				}

				RelicModel? recovered = await TryKeepRelicWithoutHistory(
					player,
					recoveryCopy,
					sourceId,
					runAfterObtained: recoveryCopy is not DustyTome,
					context: "Event relic copy recovery",
					failureIsDesyncRisk: true);
				if (recovered != null)
				{
					Log.Error(
						$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Recovered an event relic copy after its normal obtain path failed; "
						+ "inventory was preserved but pickup side effects may differ between peers: "
						+ $"player={player.NetId} relic={sourceId.Entry} "
						+ $"error={exception.GetType().Name}: {exception.Message}");
				}
			}
		}
	}

	private static RelicModel? TryCreateEventRelicRecoveryCopy(
		RelicModel sourceRelic,
		ModelId sourceId,
		out Exception? failure)
	{
		failure = null;
		try
		{
			if (sourceId == ModelId.none
				|| ModelDb.GetByIdOrNull<RelicModel>(sourceId) is not { } canonical)
			{
				return null;
			}

			RelicModel copy = canonical.ToMutable();
			CopyWaxState(sourceRelic, copy);
			if (sourceRelic is DustyTome sourceTome && copy is DustyTome copyTome)
			{
				if (sourceTome.AncientCard is not { } ancientCardId)
				{
					return null;
				}

				copyTome.AncientCard = ancientCardId;
			}

			return copy;
		}
		catch (Exception exception)
		{
			failure = exception;
			return null;
		}
	}

	internal static object? BeginDirectPotionReward(Player player)
	{
		return BeginDirectCommandReward(player);
	}

	internal static Task<PotionProcureResult> CompleteDirectPotionRewardAsync(Task<PotionProcureResult> originalTask, object? duplicationState)
	{
		if (duplicationState is not DirectCommandRewardScope scope)
		{
			return originalTask;
		}

		RestoreCommandRewardScope(scope);
		return CompleteDirectPotionRewardAsync(originalTask, scope);
	}

	internal static object? BeginDirectGoldReward(Player player, decimal amount, bool wasStolenBack)
	{
		DirectCommandRewardScope? scope = BeginDirectCommandReward(player);
		if (scope == null)
		{
			return null;
		}

		scope.GoldAmount = amount;
		scope.WasGoldStolenBack = wasStolenBack;
		return scope;
	}

	internal static Task CompleteDirectGoldRewardAsync(Task originalTask, object? duplicationState)
	{
		if (duplicationState is not DirectCommandRewardScope scope)
		{
			return originalTask;
		}

		RestoreCommandRewardScope(scope);
		return CompleteDirectGoldRewardAsync(originalTask, scope);
	}

	internal static Task<bool> CompleteRelicRewardAsync(RelicReward reward, Task<bool> originalTask, object? duplicationState)
	{
		if (duplicationState is not RewardDuplicationScope scope)
		{
			return originalTask;
		}

		return CompleteRelicRewardAsync(reward, originalTask, scope);
	}

	internal static void TrackCardPileAdd(CardModel card, PileType newPileType, AbstractModel? clonedBy, ref Task<CardPileAddResult> resultTask)
	{
		CardRewardTracker? tracker = CurrentCardRewardTracker.Value;
		if (tracker != null)
		{
			if (newPileType == PileType.Deck
				&& clonedBy is not DoubleVisionRune
				&& card.Owner == tracker.Player)
			{
				resultTask = TrackCardPileAddAsync(resultTask, tracker);
			}

			return;
		}

		if (!ShouldDuplicateDirectDeckCard(card, newPileType, clonedBy))
		{
			return;
		}

		resultTask = CompleteDirectDeckCardRewardAsync(resultTask);
	}

	private static async Task<bool> CompleteCardRewardAddTrackingAsync(Task<bool> originalTask, CardRewardTrackingScope scope)
	{
		bool rewardComplete = await originalTask;

		if (!ShouldDuplicateTrackedCardRewards(rewardComplete, scope.Tracker.AddedCards.Count))
		{
			return rewardComplete;
		}

		foreach (DoubleVisionRune rune in scope.Runes)
		{
			await rune.DuplicateRewardCards(scope.Tracker.AddedCards);
		}

		return rewardComplete;
	}

	internal static bool ShouldDuplicateTrackedCardRewards(bool rewardComplete, int addedCardCount)
	{
		// 手快全拿允许先领取若干张卡再以“跳过”结束，此时原版返回 false，但已经加入牌组的卡仍是有效奖励。
		return addedCardCount > 0;
	}

	private static async Task<bool> CompleteRelicRewardAsync(RelicReward reward, Task<bool> originalTask, RewardDuplicationScope scope)
	{
		bool rewardComplete = await originalTask;
		if (!rewardComplete || reward.ClaimedRelic == null)
		{
			return rewardComplete;
		}

		foreach (DoubleVisionRune rune in scope.Runes)
		{
			await rune.DuplicateRelicReward(reward.Player, reward);
		}

		return rewardComplete;
	}

	private static async Task<RelicModel> CompleteDirectRelicRewardAsync(Task<RelicModel> originalTask, DirectCommandRewardScope scope)
	{
		RelicModel obtainedRelic = await originalTask;
		if (obtainedRelic.Owner == null || obtainedRelic.Owner.Creature.IsDead)
		{
			return obtainedRelic;
		}

		foreach (DoubleVisionRune rune in scope.Runes)
		{
			await rune.DuplicateObtainedRelic(obtainedRelic.Owner, obtainedRelic);
		}

		return obtainedRelic;
	}

	private static async Task<PotionProcureResult> CompleteDirectPotionRewardAsync(Task<PotionProcureResult> originalTask, DirectCommandRewardScope scope)
	{
		PotionProcureResult result = await originalTask;
		if (!result.success || result.potion.Owner == null || result.potion.Owner.Creature.IsDead)
		{
			return result;
		}

		foreach (DoubleVisionRune rune in scope.Runes)
		{
			await rune.DuplicateObtainedPotion(result.potion.Owner, result.potion);
		}

		return result;
	}

	private static async Task CompleteDirectGoldRewardAsync(Task originalTask, DirectCommandRewardScope scope)
	{
		await originalTask;

		int amount = (int)scope.GoldAmount;
		if (amount <= 0 || scope.Player.Creature.IsDead)
		{
			return;
		}

		foreach (DoubleVisionRune rune in scope.Runes)
		{
			await rune.DuplicateGoldAmount(scope.Player, amount, scope.WasGoldStolenBack);
		}
	}

	private static async Task<CardPileAddResult> CompleteDirectDeckCardRewardAsync(Task<CardPileAddResult> originalTask)
	{
		CardPileAddResult result = await originalTask;
		if (!result.success
			|| result.cardAdded.Pile?.Type != PileType.Deck
			|| result.cardAdded.Owner == null
			|| !ShouldDuplicateForPlayer(result.cardAdded.Owner))
		{
			return result;
		}

		IReadOnlyList<DoubleVisionRune> runes = GetActiveRunes(result.cardAdded.Owner);
		foreach (DoubleVisionRune rune in runes)
		{
			await rune.DuplicateRewardCards(new[] { result.cardAdded });
		}

		return result;
	}

	private static async Task<CardPileAddResult> TrackCardPileAddAsync(Task<CardPileAddResult> originalTask, CardRewardTracker tracker)
	{
		CardPileAddResult result = await originalTask;
		if (result.success
			&& result.cardAdded.Owner == tracker.Player
			&& result.cardAdded.Pile?.Type == PileType.Deck)
		{
			tracker.AddedCards.Add(result.cardAdded);
		}

		return result;
	}
}
