using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static async Task SynchronizeActSelectionApplied(
		RunState runState,
		PlayerChoiceSynchronizer synchronizer,
		int actIndex,
		int choiceOrdinal,
		CancellationToken cancellationToken)
	{
		RunManager runManager = RunManager.Instance;
		List<Task> pendingAcks = [];
		foreach (Player player in runState.Players)
		{
			cancellationToken.ThrowIfCancellationRequested();
			uint choiceId = synchronizer.ReserveChoiceId(player);
			if (IsLocalPlayer(runManager, player))
			{
				uint sentChoiceId = SyncLocalHextechChoice(
					synchronizer,
					player,
					choiceId,
					HextechChoiceCodec.CreateActSelectionApplied(actIndex, choiceOrdinal),
					$"act-selection-applied act={actIndex} ordinal={choiceOrdinal}");
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied sync local: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={sentChoiceId}");
				continue;
			}

			pendingAcks.Add(WaitForRemoteActSelectionApplied(
				synchronizer,
				runState,
				player,
				choiceId,
				actIndex,
				choiceOrdinal,
				cancellationToken));
		}

		if (pendingAcks.Count == 0)
		{
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied waiting: act={actIndex} ordinal={choiceOrdinal} remoteCount={pendingAcks.Count}");
		await Task.WhenAll(pendingAcks);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied complete: act={actIndex} ordinal={choiceOrdinal}");
	}

	private static async Task WaitForRemoteActSelectionApplied(
		PlayerChoiceSynchronizer synchronizer,
		RunState runState,
		Player player,
		uint choiceId,
		int actIndex,
		int choiceOrdinal,
		CancellationToken cancellationToken)
	{
		(PlayerChoiceResult remoteAck, uint receivedChoiceId) = await WaitForRemoteHextechChoice(
			synchronizer,
			runState,
			player,
			choiceId,
			result => HextechChoiceCodec.TryDecodeActSelectionApplied(result, actIndex, choiceOrdinal),
			$"act-selection-applied act={actIndex} ordinal={choiceOrdinal}",
			cancellationToken: cancellationToken);
		if (!HextechChoiceCodec.TryDecodeActSelectionApplied(remoteAck, actIndex, choiceOrdinal))
		{
			throw new HextechChoiceProtocolException(
				$"Malformed act-selection-applied ack: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={choiceId}");
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied remote: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={receivedChoiceId}");
	}

	private static async Task WaitForFramesOrRunChangeAsync(
		RunState runState,
		int frameCount,
		CancellationToken cancellationToken = default)
	{
		TimeSpan timeout = GetNetworkChoiceTimeoutDuration(frameCount);
		if (timeout <= TimeSpan.Zero)
		{
			return;
		}

		DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
		while (!cancellationToken.IsCancellationRequested
			&& IsCurrentRun(runState)
			&& IsMultiplayerConnected()
			&& DateTimeOffset.UtcNow < deadline)
		{
			// Multiplayer timer mods can accelerate process frames; keep network choice
			// fallbacks on wall time so clients do not resolve different selection state.
			await WaitForProcessFrameOrDelayAsync(cancellationToken);
		}

		cancellationToken.ThrowIfCancellationRequested();
	}

	private static async Task WaitForRunChangeOrMultiplayerDisconnectAsync(RunState runState, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && IsCurrentRun(runState) && IsMultiplayerConnected())
		{
			try
			{
				await WaitForProcessFrameOrDelayAsync(cancellationToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}

	private static bool IsMultiplayerConnected()
	{
		INetGameService netService = RunManager.Instance.NetService;
		return netService.Type is NetGameType.Host or NetGameType.Client && netService.IsConnected;
	}

	internal static TimeSpan GetNetworkChoiceTimeoutDuration(int frameCount)
	{
		return frameCount <= 0
			? TimeSpan.Zero
			: TimeSpan.FromSeconds(frameCount / 60.0d);
	}

	internal static Task<PlayerChoiceSynchronizer> WaitForPlayerChoiceSynchronizerAsync(RunManager runManager)
	{
		PlayerChoiceSynchronizer? synchronizer = runManager.PlayerChoiceSynchronizer;
		if (synchronizer != null)
		{
			return Task.FromResult(synchronizer);
		}

		const string message = "PlayerChoiceSynchronizer is unavailable during an active multiplayer transaction.";
		AbortMultiplayerChoiceTransaction("player-choice-synchronizer", message);
		throw new HextechChoiceProtocolException(message);
	}

	internal static bool IsLocalPlayer(RunManager runManager, Player player)
	{
		return LocalContext.IsMe(player)
			|| (player.NetId != 0UL && player.NetId == runManager.NetService.NetId);
	}
}
