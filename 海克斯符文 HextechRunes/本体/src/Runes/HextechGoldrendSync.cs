using MegaCrit.Sts2.Core.Entities.Gold;

namespace HextechRunes;

internal static class HextechGoldrendSync
{
	private const int GoldrendStealAmount = 20;

	private static readonly Dictionary<ulong, LocalGoldLossTransaction> PendingLocalCombatGoldLosses = new();
	private static readonly SemaphoreSlim ApplyGate = new(1, 1);
	private static RunState? _trackedRunState;

	public static void ResetForRun(RunState runState)
	{
		LogDiscardedTransactions("run reset");
		PendingLocalCombatGoldLosses.Clear();
		_trackedRunState = runState;
	}

	public static void BeginCombat(RunState runState)
	{
		if (ReferenceEquals(_trackedRunState, runState))
		{
			return;
		}

		LogDiscardedTransactions("combat entered with a different run");
		PendingLocalCombatGoldLosses.Clear();
		_trackedRunState = runState;
	}

	public static void ClearRun(RunState? runState)
	{
		if (runState != null
			&& _trackedRunState != null
			&& !ReferenceEquals(_trackedRunState, runState))
		{
			return;
		}

		LogDiscardedTransactions("run ended");
		PendingLocalCombatGoldLosses.Clear();
		_trackedRunState = null;
	}

	public static async Task HandleEnemyGoldrendHit(Player targetPlayer)
	{
		NetGameType gameType = RunManager.Instance.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			int singlePlayerAmount = Math.Min(GoldrendStealAmount, Math.Max(0, targetPlayer.Gold));
			if (singlePlayerAmount > 0)
			{
				await PlayerCmd.LoseGold(singlePlayerAmount, targetPlayer, GoldLossType.Lost);
			}

			return;
		}

		if (gameType is not (NetGameType.Host or NetGameType.Client))
		{
			return;
		}

		if (targetPlayer.NetId == RunManager.Instance.NetService.NetId)
		{
			TrackPendingLocalGoldLoss(targetPlayer);
		}
	}

	public static async Task ApplyPendingCombatGoldLosses(RunState runState)
	{
		await ApplyGate.WaitAsync();
		try
		{
			await ApplyPendingCombatGoldLossesCore(runState);
		}
		finally
		{
			ApplyGate.Release();
		}
	}

	private static async Task ApplyPendingCombatGoldLossesCore(RunState runState)
	{
		if (PendingLocalCombatGoldLosses.Count == 0)
		{
			return;
		}

		if (!ReferenceEquals(_trackedRunState, runState))
		{
			Log.Error($"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Refusing to apply transactions outside their tracked run.");
			return;
		}

		foreach (ulong targetNetId in PendingLocalCombatGoldLosses.Keys.ToArray())
		{
			if (!PendingLocalCombatGoldLosses.TryGetValue(targetNetId, out LocalGoldLossTransaction? transaction))
			{
				continue;
			}

			Player? targetPlayer = runState.Players.FirstOrDefault(player => player.NetId == targetNetId);
			if (targetPlayer == null || targetPlayer.NetId != RunManager.Instance.NetService.NetId)
			{
				Log.Error(
					$"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Retaining transaction for unavailable local player "
					+ $"netId={targetNetId} pending={transaction.PendingAmount} localApplied={transaction.LocalAppliedAmount}.");
				continue;
			}

			if (!CanBroadcastGoldLoss())
			{
				Log.Warn(
					$"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Retaining transaction while multiplayer is disconnected "
					+ $"netId={targetNetId} pending={transaction.PendingAmount} localApplied={transaction.LocalAppliedAmount}.");
				continue;
			}

			if (transaction.LocalAppliedAmount > 0 && !TryBroadcastAppliedGoldLoss(targetNetId, transaction))
			{
				continue;
			}

			if (!CanBroadcastGoldLoss())
			{
				Log.Warn(
					$"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Retaining pending transaction after connection changed "
					+ $"netId={targetNetId} pending={transaction.PendingAmount}.");
				continue;
			}

			int pendingAmount = transaction.PendingAmount;
			transaction.PendingAmount = 0;
			int amount = Math.Min(pendingAmount, Math.Max(0, targetPlayer.Gold));
			if (amount > 0)
			{
				try
				{
					await PlayerCmd.LoseGold(amount, targetPlayer, GoldLossType.Lost);
					transaction.LocalAppliedAmount += amount;
				}
				catch
				{
					transaction.PendingAmount += pendingAmount;
					throw;
				}
			}

			if (transaction.LocalAppliedAmount > 0)
			{
				_ = TryBroadcastAppliedGoldLoss(targetNetId, transaction);
			}

			if (transaction.PendingAmount == 0 && transaction.LocalAppliedAmount == 0)
			{
				PendingLocalCombatGoldLosses.Remove(targetNetId);
			}
		}
	}

	private static void TrackPendingLocalGoldLoss(Player targetPlayer)
	{
		if (targetPlayer.RunState is not RunState runState)
		{
			return;
		}

		BeginCombat(runState);
		if (!PendingLocalCombatGoldLosses.TryGetValue(targetPlayer.NetId, out LocalGoldLossTransaction? transaction))
		{
			transaction = new LocalGoldLossTransaction();
			PendingLocalCombatGoldLosses[targetPlayer.NetId] = transaction;
		}

		int alreadyPending = transaction.PendingAmount;
		int remainingGold = Math.Max(0, targetPlayer.Gold - alreadyPending);
		int amount = Math.Min(GoldrendStealAmount, remainingGold);
		if (amount <= 0)
		{
			return;
		}

		transaction.PendingAmount = alreadyPending + amount;
	}

	private static bool CanBroadcastGoldLoss()
	{
		var netService = RunManager.Instance.NetService;
		return netService.Type is NetGameType.Host or NetGameType.Client && netService.IsConnected;
	}

	private static bool TryBroadcastAppliedGoldLoss(ulong targetNetId, LocalGoldLossTransaction transaction)
	{
		if (!CanBroadcastGoldLoss())
		{
			Log.Warn(
				$"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Retaining locally applied transaction before broadcast "
				+ $"because multiplayer is disconnected netId={targetNetId} amount={transaction.LocalAppliedAmount}.");
			return false;
		}

		int amount = transaction.LocalAppliedAmount;
		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(amount);
			// 原版接口没有 ack；正常返回只表示本地发送调用已被接受，不能证明远端已经应用。
			transaction.LocalAppliedAmount -= amount;
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Gold loss delivery is unconfirmed; "
				+ $"retaining locally applied transaction netId={targetNetId} amount={amount}: {ex}");
			return false;
		}
	}

	private static void LogDiscardedTransactions(string reason)
	{
		int pending = PendingLocalCombatGoldLosses.Values.Sum(static transaction => transaction.PendingAmount);
		int localApplied = PendingLocalCombatGoldLosses.Values.Sum(static transaction => transaction.LocalAppliedAmount);
		if (pending == 0 && localApplied == 0)
		{
			return;
		}

		Log.Warn(
			$"[{ModInfo.Id}][DESYNC-RISK][Goldrend] Clearing unresolved run-scoped transactions "
			+ $"reason={reason} pending={pending} localApplied={localApplied}.");
	}

	private sealed class LocalGoldLossTransaction
	{
		public int PendingAmount { get; set; }

		public int LocalAppliedAmount { get; set; }
	}
}
