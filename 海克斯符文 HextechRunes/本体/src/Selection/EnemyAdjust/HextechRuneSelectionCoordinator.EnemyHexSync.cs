using MegaCrit.Sts2.Core.GameActions;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static EnemyHexAdjustmentSyncContext? CreateEnemyHexAdjustmentSyncContext(
		RunManager runManager,
		RunState runState,
		PlayerChoiceSynchronizer synchronizer,
		int actIndex,
		IReadOnlyList<MonsterHexKind> initialMonsterHexes)
	{
		Player? authorityPlayer = GetActRollAuthorityPlayer(runManager, runState);
		if (authorityPlayer == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] EnemyHexAdjustmentSync: no authority player act={actIndex}");
			return null;
		}

		uint choiceId = synchronizer.ReserveChoiceId(authorityPlayer);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyHexAdjustmentSync: reserved act={actIndex} authority={authorityPlayer.NetId} choiceId={choiceId}");
		return new EnemyHexAdjustmentSyncContext(synchronizer, authorityPlayer, choiceId, actIndex, initialMonsterHexes);
	}

	private static HextechEnemyHexAdjustmentOptions? CreateEnemyHexAdjustmentOptionsForSelection(
		HextechMayhemModifier modifier,
		RunManager runManager,
		RunState runState,
		int actIndex,
		HextechRarityTier rarity,
		IReadOnlyList<MonsterHexKind> activeMonsterHexes,
		IReadOnlyList<MonsterHexKind> initialNewMonsterHexes,
		IReadOnlySet<ModelId> enemyRerollExcludedIds,
		EnemyHexAdjustmentSyncContext? syncContext,
		PendingRuneSelection selection,
		CancellationToken cancellationToken)
	{
		if (!selection.IsLocal || (syncContext == null && activeMonsterHexes.Count == 0))
		{
			return null;
		}

		bool isAuthorityLocal = syncContext != null && IsLocalPlayer(runManager, syncContext.AuthorityPlayer);
		HashSet<MonsterHexKind> seenEnemyHexes = modifier.GetKnownMonsterHexes().ToHashSet();
		seenEnemyHexes.UnionWith(syncContext?.CurrentMonsterHexes ?? initialNewMonsterHexes);
		return new HextechEnemyHexAdjustmentOptions
		{
			// choiceOrdinal>0 时无新 hex 可调整(syncContext 为 null):只读展示【本幕新增】的敌方 hex。
			// 不能回退到 activeMonsterHexes(前几幕累积集),否则会把历史敌方海克斯一起显示(玩家实报);
			// 本幕无新增时面板按空集隐藏。纯显示、不触发任何同步。
			InitialHexes = syncContext?.CurrentMonsterHexes ?? initialNewMonsterHexes,
			ExcludedHexes = activeMonsterHexes,
			RerollLimit = modifier.MonsterHexRerollLimit,
			ControlsEnabled = isAuthorityLocal,
			RerollFunc = isAuthorityLocal
				? (currentHexes, slotIndex, rerollOrdinal) => RerollEnemyHexForAct(
					modifier,
					rarity,
					runState,
					actIndex,
					GetMonsterHexSlot(currentHexes, slotIndex),
					rerollOrdinal,
					CreateEnemyHexRerollExcludedIds(enemyRerollExcludedIds, currentHexes, slotIndex),
					seenEnemyHexes)
				: null,
			Changed = isAuthorityLocal && syncContext != null
				? (monsterHexes, rerollCounts) => SendEnemyHexAdjustment(syncContext, monsterHexes, rerollCounts, isFinal: false)
				: null,
			ScreenCreated = !isAuthorityLocal && syncContext != null
				? screen => syncContext.RemoteReceiveTask = ReceiveEnemyHexAdjustments(syncContext, runState, screen, cancellationToken)
				: null
		};
	}

	private static async Task CompleteLocalEnemyHexAdjustmentSync(RunManager runManager, EnemyHexAdjustmentSyncContext? syncContext, HextechRuneSelectionScreen screen)
	{
		if (syncContext == null)
		{
			return;
		}

		if (IsLocalPlayer(runManager, syncContext.AuthorityPlayer))
		{
			SendEnemyHexAdjustment(syncContext, screen.CurrentMonsterHexSlots, screen.EnemyHexRerollCounts, isFinal: true);
			return;
		}

		if (syncContext.RemoteReceiveTask != null)
		{
			await syncContext.RemoteReceiveTask;
		}
	}

	private static bool SendEnemyHexAdjustment(
		EnemyHexAdjustmentSyncContext syncContext,
		IReadOnlyList<MonsterHexKind?> monsterHexes,
		IReadOnlyList<int> rerollCounts,
		bool isFinal)
	{
		if (syncContext.FinalSent)
		{
			return true;
		}

		try
		{
			List<MonsterHexKind?> nextMonsterHexes = monsterHexes.ToList();
			List<int> nextRerollCounts = rerollCounts.Select(static count => Math.Max(0, count)).ToList();
			EnemyHexAdjustmentPayload payload = new(
				syncContext.ActIndex,
				syncContext.Sequence,
				nextMonsterHexes.ToArray(),
				nextRerollCounts.ToArray(),
				isFinal);
			int operationToken = GetEnemyHexAdjustmentOperationToken(syncContext);
			uint sentChoiceId = SyncLocalHextechChoice(
				syncContext.Synchronizer,
				syncContext.AuthorityPlayer,
				syncContext.NextChoiceId,
				HextechChoiceCodec.CreateEnemyHexAdjustment(operationToken, payload),
				$"enemy-hex-adjustment act={syncContext.ActIndex}");

			syncContext.CurrentMonsterHexSlots.Clear();
			syncContext.CurrentMonsterHexSlots.AddRange(nextMonsterHexes);
			syncContext.RerollCounts.Clear();
			syncContext.RerollCounts.AddRange(nextRerollCounts);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyHexAdjustmentSync send: act={syncContext.ActIndex} choiceId={sentChoiceId} seq={syncContext.Sequence} hexes={string.Join(",", syncContext.CurrentMonsterHexSlots.Select(static hex => hex?.ToString() ?? "None"))} rerolls={string.Join(",", syncContext.RerollCounts)} final={isFinal}");
			if (isFinal)
			{
				syncContext.FinalSent = true;
				return true;
			}

			syncContext.Sequence++;
			syncContext.NextChoiceId = syncContext.Synchronizer.ReserveChoiceId(syncContext.AuthorityPlayer);
			return true;
		}
		catch (HextechChoiceProtocolException)
		{
			throw;
		}
		catch (Exception ex)
		{
			string message =
				$"Enemy hex adjustment failed after reserving choice: act={syncContext.ActIndex} " +
				$"player={syncContext.AuthorityPlayer.NetId} choiceId={syncContext.NextChoiceId} " +
				$"sequence={syncContext.Sequence}";
			throw CreateProtocolFailure($"enemy-hex-adjustment act={syncContext.ActIndex}", message, ex);
		}
	}

	private static async Task ObserveEnemyHexAdjustmentReceiveTask(EnemyHexAdjustmentSyncContext? syncContext)
	{
		Task? receiveTask = syncContext?.RemoteReceiveTask;
		if (receiveTask == null)
		{
			return;
		}

		try
		{
			await receiveTask;
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][Mayhem] Enemy hex adjustment receiver failed during transaction cleanup: " +
				$"act={syncContext!.ActIndex} error={ex}");
		}
	}

	private static async Task ReceiveEnemyHexAdjustments(
		EnemyHexAdjustmentSyncContext syncContext,
		RunState runState,
		HextechRuneSelectionScreen screen,
		CancellationToken cancellationToken)
	{
		while (screen.IsInsideTree() && !cancellationToken.IsCancellationRequested)
		{
			int operationToken = GetEnemyHexAdjustmentOperationToken(syncContext);
			(PlayerChoiceResult result, uint receivedChoiceId)? received = await TryWaitForRemoteHextechChoice(
				syncContext.Synchronizer,
				runState,
				syncContext.AuthorityPlayer,
				syncContext.NextChoiceId,
				choice => HextechChoiceCodec.TryDecodeEnemyHexAdjustment(
					choice,
					operationToken,
					syncContext.ActIndex,
					syncContext.Sequence,
					out _),
				$"enemy-hex-adjustment act={syncContext.ActIndex}",
				RemoteRuneChoicePollFrames,
				() => screen.IsInsideTree() && IsCurrentRun(runState) && IsMultiplayerConnected(),
				cancellationToken: cancellationToken);
			if (!received.HasValue)
			{
				Log.Warn(
					$"[{ModInfo.Id}][Mayhem] EnemyHexAdjustmentSync interrupted: " +
					$"act={syncContext.ActIndex} choiceId={syncContext.NextChoiceId} " +
					$"screenActive={screen.IsInsideTree()} runActive={IsCurrentRun(runState)} connected={IsMultiplayerConnected()}");
				return;
			}

			(PlayerChoiceResult result, uint receivedChoiceId) = received.Value;
			if (!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(
				result,
				operationToken,
				syncContext.ActIndex,
				syncContext.Sequence,
				out EnemyHexAdjustmentPayload payload))
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] EnemyHexAdjustmentSync malformed: act={syncContext.ActIndex} choiceId={receivedChoiceId}");
				return;
			}

			syncContext.CurrentMonsterHexSlots.Clear();
			syncContext.CurrentMonsterHexSlots.AddRange(payload.MonsterHexes);
			syncContext.RerollCounts.Clear();
			syncContext.RerollCounts.AddRange(payload.RerollCounts.Select(static count => Math.Max(0, count)));
			syncContext.Sequence = payload.Sequence + 1;
			screen.ApplyEnemyHexAdjustment(payload.MonsterHexes, payload.RerollCounts);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyHexAdjustmentSync receive: act={syncContext.ActIndex} choiceId={receivedChoiceId} seq={payload.Sequence} hexes={string.Join(",", payload.MonsterHexes.Select(static hex => hex?.ToString() ?? "None"))} rerolls={string.Join(",", payload.RerollCounts)} final={payload.IsFinal}");
			if (payload.IsFinal)
			{
				return;
			}

			syncContext.NextChoiceId = syncContext.Synchronizer.ReserveChoiceId(syncContext.AuthorityPlayer);
		}
	}

	private static int GetEnemyHexAdjustmentOperationToken(EnemyHexAdjustmentSyncContext syncContext)
	{
		return HextechChoiceCodec.ComputeOperationToken(
			"enemy-hex-adjustment",
			syncContext.NextChoiceId,
			syncContext.AuthorityPlayer.NetId,
			$"act={syncContext.ActIndex};sequence={syncContext.Sequence}");
	}
}
