using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace HextechRunes;

public sealed class DoubleVisionRune : HextechRelicBase
{
	private static readonly AsyncLocal<CardRewardTracker?> CurrentCardRewardTracker = new();
	private static readonly AsyncLocal<int> CommandDuplicationSuppressionDepth = new();
	private static readonly AsyncLocal<EventRelicTransaction?> CurrentEventRelicTransaction = new();
	private static readonly AsyncLocal<int> EventRelicObtainDepth = new();
	private static readonly AsyncLocal<DustyTome?> SuppressedDustyTomeAfterObtained = new();
	private static readonly ConditionalWeakTable<EventRoom, EventRelicTransactionBatch> EventRelicTransactionBatches = new();
	private static readonly FieldInfo? GoldRewardWasStolenBackField = typeof(GoldReward).GetField("_wasGoldStolenBack", BindingFlags.Instance | BindingFlags.NonPublic);

	// 0.109.0 以前的版本会把事件遗物写进此字段。新代码只将它作为旧存档恢复队列:
	// 正常事件获得由 EventOption.Chosen 外层事务在原选项 Task 完成后立即结算,不再新增 pending。
	private readonly List<string> _pendingEventRelicIds = new();

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedPendingEventRelicIdsJson
	{
		get => _pendingEventRelicIds.Count == 0 ? "" : string.Join(",", _pendingEventRelicIds);
		set
		{
			_pendingEventRelicIds.Clear();
			if (!string.IsNullOrEmpty(value))
			{
				_pendingEventRelicIds.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries));
			}
		}
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		// 只恢复旧版本存档留下的 pending。事件房和战斗初始化仍不是安全恢复点:
		// 华美手镯等交互式 AfterObtained 在战斗 init 开不出 UI,联机也不能在此广播旧奖励消息。
		if (room is EventRoom || room is CombatRoom || CombatManager.Instance.IsInProgress)
		{
			return Task.CompletedTask;
		}

		return FlushPendingEventRelics();
	}

	// 旧存档恰好从事件直接进入战斗时,在胜利判定后恢复 pending。
	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner == null
			|| room.CombatState == null
			|| HextechCombatCreatureHelper.GetAliveEnemies(room.CombatState).Count > 0)
		{
			return Task.CompletedTask;
		}

		return FlushPendingEventRelics();
	}

	private async Task FlushPendingEventRelics()
	{
		if (Owner == null || _pendingEventRelicIds.Count == 0)
		{
			return;
		}

		// 联机时 SavedProperty 状态同步可能把待复制清单带到远端实例,远端只清账不复制(由持有端广播兜底)。
		if (Owner.Creature.IsDead || !ShouldDuplicateForPlayer(Owner))
		{
			_pendingEventRelicIds.Clear();
			return;
		}

		List<string> pending = new(_pendingEventRelicIds);
		foreach (string idEntry in pending)
		{
			// 旧格式只有 id,无法恢复真实实例身份;沿用旧语义,但成功处理一项后才移除,
			// 避免复制/交互抛异常时把整个恢复队列提前清空。
			RelicModel? source = Owner.Relics.FirstOrDefault(relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry == idEntry);
			if (source != null)
			{
				await DuplicateObtainedRelic(Owner, source);
			}

			_pendingEventRelicIds.Remove(idEntry);
		}
	}

	public override async Task AfterRewardTaken(Player player, Reward reward)
	{
		// 联机时奖励领取在各端都会触发本 hook:必须只在持有者本地端复制并广播,
		// 否则 N 人局的 N-1 个远端各复制一份(玩家实测 4 人局一瓶药水复制成三瓶,塞爆药水栏黑屏)。
		if (Owner == null
			|| !ReferenceEquals(player, Owner)
			|| player.Creature.IsDead
			|| !ShouldDuplicateForPlayer(player))
		{
			return;
		}

		switch (reward)
		{
			case GoldReward goldReward:
				await DuplicateGoldReward(player, goldReward);
				break;
			case PotionReward potionReward:
				await DuplicatePotionReward(player, potionReward);
				break;
			case HextechForgeChoiceReward forgeReward:
				await DuplicateForgeReward(player, forgeReward);
				break;
		}
	}

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

		if (!rewardComplete || scope.Tracker.AddedCards.Count == 0)
		{
			return rewardComplete;
		}

		foreach (DoubleVisionRune rune in scope.Runes)
		{
			await rune.DuplicateRewardCards(scope.Tracker.AddedCards);
		}

		return rewardComplete;
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

	private async Task DuplicateRewardCards(IReadOnlyList<CardModel> sourceCards)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		List<CardPileAddResult> results = new();
		foreach (CardModel sourceCard in sourceCards)
		{
			if (sourceCard.Owner != Owner || !Owner.RunState.ContainsCard(sourceCard))
			{
				continue;
			}

			CardModel copy = Owner.RunState.CloneCard(sourceCard);
			CardPileAddResult result = await RunWithCommandDuplicationSuppressed(
				() => CardPileCmd.Add(copy, PileType.Deck, clonedBy: this));
			if (!result.success)
			{
				continue;
			}

			SaveManager.Instance.MarkCardAsSeen(result.cardAdded);
			TrySyncObtainedCard(result.cardAdded);
			results.Add(result);
		}

		if (results.Count > 0)
		{
			Flash();
			CardCmd.PreviewCardPileAdd(results, 2f);
		}
	}

	private async Task DuplicateGoldReward(Player player, GoldReward reward)
	{
		if (reward.Amount <= 0)
		{
			return;
		}

		bool wasGoldStolenBack = GoldRewardWasStolenBackField?.GetValue(reward) is true;
		await DuplicateGoldAmount(player, reward.Amount, wasGoldStolenBack);
	}

	private async Task DuplicateGoldAmount(Player player, int amount, bool wasGoldStolenBack)
	{
		if (amount <= 0)
		{
			return;
		}

		Flash();
		await RunWithCommandDuplicationSuppressed(
			() => PlayerCmd.GainGold(amount, player, wasGoldStolenBack));
		TrySyncObtainedGold(amount);
	}

	private async Task DuplicatePotionReward(Player player, PotionReward reward)
	{
		PotionModel? claimedPotion = reward.ClaimedPotion;
		if (claimedPotion == null)
		{
			return;
		}

		await DuplicateObtainedPotion(player, claimedPotion);
	}

	private async Task DuplicateObtainedPotion(Player player, PotionModel sourcePotion)
	{
		PotionModel copy = ModelDb.GetById<PotionModel>(sourcePotion.CanonicalInstance?.Id ?? sourcePotion.Id).ToMutable();
		PotionProcureResult result = await RunWithCommandDuplicationSuppressed(
			() => PotionCmd.TryToProcure(copy, player));
		if (!result.success)
		{
			return;
		}

		Flash();
		TrySyncObtainedPotion(result.potion);
	}

	private async Task DuplicateRelicReward(Player player, RelicReward reward)
	{
		RelicModel? claimedRelic = reward.ClaimedRelic;
		if (claimedRelic == null)
		{
			return;
		}

		await DuplicateObtainedRelic(player, claimedRelic);
	}

	private async Task DuplicateObtainedRelic(Player player, RelicModel sourceRelic, bool syncReward = true)
	{
		// 复视不复制海克斯模组自己的符文/遗物/锻造；原版及已注册的外部遗物仍按其模型 ID 复制。
		// 复杂且对多人敏感,重复获得易引发分叉/卡死(玩家实测黑屏的一类来源)。按需求收窄复视作用域为原版遗物。
		// 判据取并,覆盖本体 + 拓展包(HextechRunesSponsorPack)且不硬引用拓展包程序集:
		//   ① 继承 HextechRelicBase 的——本体+拓展包的符文、以及 HextechForgeBase 锻造;
		//   ② 程序集名以 "HextechRunes" 开头的——覆盖拓展包里直接继承 RelicModel 的事件遗物(如 GoldStarRelic)。
		// 原版遗物程序集名为 "sts2" 且非 HextechRelicBase,故不受影响,复视照常复制。
		if (sourceRelic is HextechRelicBase
			|| sourceRelic.GetType().Assembly.GetName().Name?.StartsWith("HextechRunes", StringComparison.Ordinal) == true)
		{
			return;
		}

		if (sourceRelic is DustyTome dustyTome)
		{
			await DuplicateDustyTome(player, dustyTome, syncReward);
			return;
		}

		// 复视不对 Orobas 先古遗物「古老牙齿」「欧洛巴斯之触」生效（不复制它们）：
		// 它们的获得/转化流程不适合被复制（古老牙齿重复获得会因牌组无可转化牌而卡死）。
		// 黄金罗盘（GoldenCompass）同样跳过：其 AfterObtained 会 await RunManager.GenerateMap() 重建全图、
		// 消耗共享 RNG 并改写共享地图。即使事件事务在所有端执行,多人各玩家事件任务仍可能并发完成,
		// 不能把全局地图重建挂到玩家级奖励事务里。
		// 先古遗物本就不该被双倍，复制出第二枚黄金罗盘语义上也不成立。
		if (sourceRelic is ArchaicTooth or TouchOfOrobas or GoldenCompass)
		{
			return;
		}

		ModelId sourceId = sourceRelic.CanonicalInstance?.Id ?? sourceRelic.Id;
		RelicModel? canonical = sourceId == ModelId.none
			? null
			: ModelDb.GetByIdOrNull<RelicModel>(sourceId);
		if (canonical == null)
		{
			Log.Warn(
				$"[{ModInfo.Id}][DoubleVision] Skipped relic duplication because its model is not registered: "
				+ $"player={player.NetId} relic={sourceId.Entry} type={sourceRelic.GetType().FullName}.");
			return;
		}

		RelicModel copy = canonical.ToMutable();
		CopyWaxState(sourceRelic, copy);
		RelicModel obtained = await RunWithCommandDuplicationSuppressed(
			() => RelicCmd.Obtain(copy, player));
		if (LocalContext.IsMe(player))
		{
			Flash();
		}
		if (syncReward)
		{
			TrySyncObtainedRelic(obtained);
		}
	}

	private async Task DuplicateDustyTome(Player player, DustyTome sourceTome, bool syncReward)
	{
		if (sourceTome.AncientCard == null)
		{
			Log.Warn($"[{ModInfo.Id}][DoubleVision] Refused to duplicate Dusty Tome without an AncientCard.");
			return;
		}

		DustyTome obtained = await DuplicateDustyTomeSpecialized(
			sourceTome,
			syncReward,
			copy => RunWithCommandDuplicationSuppressed(async () =>
				(DustyTome)await RelicCmd.Obtain(copy, player)),
			static copy => TrySyncObtainedRelic(copy));
		if (LocalContext.IsMe(player))
		{
			Flash();
		}
	}

	internal static Task<T> DuplicateDustyTomeSpecialized<T>(
		DustyTome sourceTome,
		bool syncReward,
		Func<DustyTome, Task<T>> obtainCopy,
		Action<T> synchronize)
	{
		return DuplicateDustyTomeSpecializedCore(
			sourceTome,
			syncReward,
			obtainCopy,
			synchronize,
			createCopy: null,
			assignAncientCard: null);
	}

	internal static Task<T> DuplicateDustyTomeSpecializedForTest<T>(
		DustyTome sourceTome,
		bool syncReward,
		Func<DustyTome, Task<T>> obtainCopy,
		Action<T> synchronize,
		Func<DustyTome> createCopy,
		Action<DustyTome, ModelId> assignAncientCard)
	{
		return DuplicateDustyTomeSpecializedCore(
			sourceTome,
			syncReward,
			obtainCopy,
			synchronize,
			createCopy,
			assignAncientCard);
	}

	private static async Task<T> DuplicateDustyTomeSpecializedCore<T>(
		DustyTome sourceTome,
		bool syncReward,
		Func<DustyTome, Task<T>> obtainCopy,
		Action<T> synchronize,
		Func<DustyTome>? createCopy,
		Action<DustyTome, ModelId>? assignAncientCard)
	{
		ArgumentNullException.ThrowIfNull(sourceTome);
		ArgumentNullException.ThrowIfNull(obtainCopy);
		ArgumentNullException.ThrowIfNull(synchronize);
		if (sourceTome.AncientCard is not { } ancientCardId)
		{
			throw new InvalidOperationException("Cannot duplicate Dusty Tome without an AncientCard.");
		}

		DustyTome copy = createCopy?.Invoke()
			?? (DustyTome)ModelDb
				.GetById<RelicModel>(sourceTome.CanonicalInstance?.Id ?? sourceTome.Id)
				.ToMutable();
		CopyWaxState(sourceTome, copy);
		assignAncientCard ??= static (dustyTome, cardId) => dustyTome.AncientCard = cardId;
		assignAncientCard(copy, ancientCardId);
		T obtained = await RunWithDustyTomeAfterObtainedSuppressed(
			copy,
			() => obtainCopy(copy));
		if (syncReward)
		{
			synchronize(obtained);
		}

		return obtained;
	}

	internal static void CopyWaxState(RelicModel source, RelicModel copy)
	{
		copy.IsWax = source.IsWax;
	}

	internal static bool ShouldSuppressDustyTomeAfterObtained(DustyTome dustyTome)
	{
		return ReferenceEquals(SuppressedDustyTomeAfterObtained.Value, dustyTome);
	}

	private static async Task<T> RunWithDustyTomeAfterObtainedSuppressed<T>(
		DustyTome dustyTome,
		Func<Task<T>> action)
	{
		DustyTome? previous = SuppressedDustyTomeAfterObtained.Value;
		SuppressedDustyTomeAfterObtained.Value = dustyTome;
		try
		{
			return await action();
		}
		finally
		{
			SuppressedDustyTomeAfterObtained.Value = previous;
		}
	}

	private async Task DuplicateForgeReward(Player player, HextechForgeChoiceReward reward)
	{
		await DuplicateForgeById(player, reward.ClaimedForgeId);
	}

	// 商店购买的属性锻造器不走 AfterRewardTaken/HextechForgeChoiceReward,而直接 RelicCmd.Obtain 又会被
	// DuplicateObtainedRelic 的「本模组程序集」闸门跳过(锻造器全是本模组类型),因此复视此前复制不到商店锻造器。
	// 由商店购买流程在成功获得后显式调用本入口,为玩家持有的每个复视各复制一份,复用与锻造奖励完全相同的
	// ObtainSelectedForge(syncObtainedRelic) 路径。GetActiveRunes 已含「本地持有者」联机闸门(远端由广播兜底)。
	internal static async Task DuplicatePurchasedForge(Player player, RelicModel forge)
	{
		ModelId forgeId = forge.CanonicalInstance?.Id ?? forge.Id;
		if (forgeId == ModelId.none)
		{
			return;
		}

		foreach (DoubleVisionRune rune in GetActiveRunes(player))
		{
			await rune.DuplicateForgeById(player, forgeId);
		}
	}

	private async Task DuplicateForgeById(Player player, ModelId forgeId)
	{
		// (B1)附魔锻造的 AfterObtained 会开交互式选牌(FromDeckForEnchantment),必须走「持有者开UI+SyncLocalChoice、
		// 远端 WaitForRemoteChoice」的选择同步协议(每次选牌都 ReserveChoiceId)。复制份只能在【本地持有者】这一端
		// 真正开第二次选牌,再靠 ObtainSelectedForge 的 syncObtainedRelic 广播让远端经 RewardSynchronizer 获得这份
		// 锻造并 WaitForRemoteChoice 回放同一选牌——这是原版 HextechForgeChoiceReward.OnSelect(仅选取端运行)的镜像。
		// 缺这道本地闸门时,远端也各自跑 ObtainSelectedForge 开自己的选牌→复制份 choiceId 与持有者错位→远端拿到
		// Index 型结果→AsDeckCards 抛异常(玩家实测黑屏/卡的来源之一)。非本地持有者直接返回,由广播兜底。
		if (forgeId == ModelId.none || !ShouldDuplicateForPlayer(player))
		{
			return;
		}

		RelicModel forge = ModelDb.GetById<RelicModel>(forgeId).ToMutable();
		Flash();
		await RunWithCommandDuplicationSuppressed(
			() => HextechForgeGrantHelper.ObtainSelectedForge(player, forge, syncObtainedRelic: true));
	}

	private static DirectCommandRewardScope? BeginDirectCommandReward(Player player)
	{
		if (IsCommandDuplicationSuppressed()
			|| CombatManager.Instance.IsInProgress
			|| !ShouldDuplicateForPlayer(player))
		{
			return null;
		}

		// 事件房不走 Direct 类内联复制。事件选项内的 RelicCmd.Obtain 由 EventOption.Chosen
		// 外层事务捕获,等原 OnChosen Task 返回后再在所有端顺序提交;不在事务内的背景命令不猜测为奖励。
		// 战后奖励屏的翻倍仍走 RelicReward.OnSelect 等 Reward 专用 hook。
		if (player.RunState.CurrentRoom is EventRoom)
		{
			return null;
		}

		IReadOnlyList<DoubleVisionRune> runes = GetActiveRunes(player);
		if (runes.Count == 0)
		{
			return null;
		}

		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		return new DirectCommandRewardScope(player, runes, previousDepth);
	}

	private static void RestoreCommandRewardScope(DirectCommandRewardScope scope)
	{
		CommandDuplicationSuppressionDepth.Value = scope.PreviousSuppressionDepth;
	}

	private static bool ShouldDuplicateDirectDeckCard(CardModel card, PileType newPileType, AbstractModel? clonedBy)
	{
		return newPileType == PileType.Deck
			&& clonedBy == null
			&& !IsCommandDuplicationSuppressed()
			&& !CombatManager.Instance.IsInProgress
			&& card.Owner != null
			&& ShouldDuplicateForPlayer(card.Owner);
	}

	private static bool IsCommandDuplicationSuppressed()
	{
		return CommandDuplicationSuppressionDepth.Value > 0;
	}

	private static async Task<T> RunWithCommandDuplicationSuppressed<T>(Func<Task<T>> action)
	{
		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		try
		{
			return await action();
		}
		finally
		{
			CommandDuplicationSuppressionDepth.Value = previousDepth;
		}
	}

	private static async Task RunWithCommandDuplicationSuppressed(Func<Task> action)
	{
		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		try
		{
			await action();
		}
		finally
		{
			CommandDuplicationSuppressionDepth.Value = previousDepth;
		}
	}

	private static IReadOnlyList<DoubleVisionRune> GetActiveRunes(Player player)
	{
		if (!ShouldDuplicateForPlayer(player))
		{
			return [];
		}

		return player.Relics
			.OfType<DoubleVisionRune>()
			.Where(static rune => rune.Owner != null)
			.ToList();
	}

	private static IReadOnlyList<DoubleVisionRune> GetEventActiveRunes(Player player)
	{
		if (player.Creature.IsDead)
		{
			return [];
		}

		return player.Relics
			.OfType<DoubleVisionRune>()
			.Where(static rune => rune.Owner != null)
			.ToList();
	}

	private static bool ShouldDuplicateForPlayer(Player player)
	{
		if (player.Creature.IsDead)
		{
			return false;
		}

		RunManager? runManager = RunManager.Instance;
		INetGameService? netService = runManager?.NetService;
		if (netService != null
			&& netService.Type is NetGameType.Host or NetGameType.Client
			&& netService.IsConnected
			&& !LocalContext.IsMe(player))
		{
			return false;
		}

		return true;
	}

	private static bool ShouldSyncReward()
	{
		RunManager? runManager = RunManager.Instance;
		INetGameService? netService = runManager?.NetService;
		return netService != null
			&& netService.Type is NetGameType.Host or NetGameType.Client
			&& netService.IsConnected;
	}

	private static void TrySyncObtainedCard(CardModel card)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(card);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated card reward was already granted, "
				+ $"but its multiplayer broadcast failed: card={card.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void TrySyncObtainedGold(int amount)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(amount);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated gold reward was already granted, "
				+ $"but its multiplayer broadcast failed: amount={amount} error={ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void TrySyncObtainedPotion(PotionModel potion)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedPotion(potion);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated potion reward was already granted, "
				+ $"but its multiplayer broadcast failed: potion={potion.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void TrySyncObtainedRelic(RelicModel relic)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(relic);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated relic reward was already granted, "
				+ $"but its multiplayer broadcast failed: relic={relic.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
		}
	}

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
