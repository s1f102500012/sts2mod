using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static class HextechRelicOptionSelectionCoordinator
{
	public static async Task<RelicModel?> SelectRelicOption(
		Player player,
		IReadOnlyList<RelicModel> options,
		string context,
		bool syncMultiplayerChoice = true)
	{
		if (options.Count == 0)
		{
			Log.Warn($"[{ModInfo.Id}][RelicOptionChoice] No options available: player={player.NetId} context={context}");
			return null;
		}

		MarkRelicsSeen(options);
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			return await SelectLocalRelic(player, options, context);
		}

		if (!syncMultiplayerChoice)
		{
			if (HextechRuneSelectionCoordinator.IsLocalPlayer(runManager, player))
			{
				return await SelectLocalRelic(player, options, context);
			}

			Log.Warn($"[{ModInfo.Id}][RelicOptionChoice] Unsynced relic option selection ignored for remote player={player.NetId} context={context}");
			return null;
		}

		PlayerChoiceSynchronizer synchronizer = await HextechRuneSelectionCoordinator.WaitForPlayerChoiceSynchronizerAsync(runManager);

		uint choiceId = synchronizer.ReserveChoiceId(player);
		int operationToken = HextechChoiceCodec.ComputeOperationToken(
			"relic-option-selection",
			choiceId,
			player.NetId,
			context);
		if (HextechRuneSelectionCoordinator.IsLocalPlayer(runManager, player))
		{
			try
			{
				RelicModel? selected = await SelectLocalRelic(player, options, context);
				if (selected == null)
				{
					uint canceledChoiceId = HextechRuneSelectionCoordinator.SyncLocalHextechChoice(
						synchronizer,
						player,
						choiceId,
						HextechChoiceCodec.CreateRelicOptionSelection(operationToken, selectedIndex: -1, options),
						$"relic-option-choice {context}");
					HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Local selection canceled: player={player.NetId} choiceId={canceledChoiceId} context={context}");
					return null;
				}

				int selectedIndex = IndexOfRelicById(options, selected);
				if (selectedIndex < 0)
				{
					string message = $"Local relic option selection is not in the synchronized option set: player={player.NetId} context={context}";
					throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"relic-option-choice {context}", message);
				}

				if (!runManager.NetService.IsConnected)
				{
					throw new OperationCanceledException(
						$"Local relic option selection ended after multiplayer disconnected: player={player.NetId} context={context}");
				}

				PlayerChoiceResult result = HextechChoiceCodec.CreateRelicOptionSelection(
					operationToken,
					selectedIndex,
					options);
				uint sentChoiceId = HextechRuneSelectionCoordinator.SyncLocalHextechChoice(
					synchronizer,
					player,
					choiceId,
					result,
					$"relic-option-choice {context}");

				HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Sync local: player={player.NetId} choiceId={sentChoiceId} index={selectedIndex} context={context}");
				return selected;
			}
			catch (HextechChoiceProtocolException)
			{
				throw;
			}
			catch (OperationCanceledException) when (!runManager.NetService.IsConnected)
			{
				throw;
			}
			catch (Exception ex)
			{
				string message =
					$"Local relic option transaction failed after reserving choice: " +
					$"player={player.NetId} choiceId={choiceId} context={context}";
				throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"relic-option-choice {context}", message, ex);
			}
		}

		HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Wait remote: player={player.NetId} choiceId={choiceId} context={context}");
		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = await HextechRuneSelectionCoordinator.WaitForRemoteHextechChoice(
			synchronizer,
			(RunState)player.RunState,
			player,
			choiceId,
			result => HextechChoiceCodec.IsRelicOptionSelection(result, operationToken, options),
			$"relic-option-choice {context}");
		HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Remote received: player={player.NetId} choiceId={receivedChoiceId} context={context}");
		return ResolveRemoteRelicOptionChoice(player, options, remoteChoice, operationToken, context);
	}

	private static async Task<RelicModel?> SelectLocalRelic(Player player, IReadOnlyList<RelicModel> options, string context)
	{
		try
		{
			if (!await WaitForOverlayStackAsync())
			{
				Log.Warn($"[{ModInfo.Id}][RelicOptionChoice] Overlay stack unavailable: player={player.NetId} context={context}");
				return null;
			}

			NChooseARelicSelection? screen = NChooseARelicSelection.ShowScreen(options);
			if (screen == null)
			{
				Log.Warn($"[{ModInfo.Id}][RelicOptionChoice] Selection screen unavailable: player={player.NetId} context={context}");
				return null;
			}

			RelicModel? selected = (await screen.RelicsSelected()).FirstOrDefault();
			HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Local selected: player={player.NetId} relic={(selected?.CanonicalInstance?.Id ?? selected?.Id)?.Entry ?? "null"} context={context}");
			return selected;
		}
		catch (OperationCanceledException)
		{
			HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Selection cancelled: player={player.NetId} context={context}");
			return null;
		}
	}

	private static async Task<bool> WaitForOverlayStackAsync()
	{
		return await WaitForSingletonAsync(static () => NOverlayStack.Instance) != null;
	}

	private static RelicModel? ResolveRemoteRelicOptionChoice(
		Player player,
		IReadOnlyList<RelicModel> expectedOptions,
		PlayerChoiceResult remoteChoice,
		int expectedOperationToken,
		string context)
	{
		string payloadDump = HextechChoiceCodec.TryGetIndexPayload(remoteChoice, out List<int> payload)
			? $"[{string.Join(",", payload)}]"
			: remoteChoice.ToString();
		if (!HextechChoiceCodec.TryDecodeRelicOptionSelection(
			remoteChoice,
			expectedOperationToken,
			out int selectedIndex,
			out List<ModelId> optionIds))
		{
			string message = $"[{ModInfo.Id}][RelicOptionChoice] Malformed payload: player={player.NetId} context={context} payload={payloadDump}";
			Log.Error(message);
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"relic-option-choice {context}", message);
		}

		if (selectedIndex == -1)
		{
			HextechLog.Info($"[{ModInfo.Id}][RelicOptionChoice] Remote selection canceled: player={player.NetId} context={context}");
			return null;
		}

		if (optionIds.Count != expectedOptions.Count)
		{
			string message =
				$"[{ModInfo.Id}][RelicOptionChoice] Synced option count mismatch: player={player.NetId} " +
				$"expected={expectedOptions.Count} actual={optionIds.Count} context={context} payload={payloadDump}";
			Log.Error(message);
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"relic-option-choice {context}", message);
		}

		if (selectedIndex < 0 || selectedIndex >= optionIds.Count)
		{
			string message = $"[{ModInfo.Id}][RelicOptionChoice] Invalid selected index: player={player.NetId} index={selectedIndex} count={optionIds.Count} context={context} payload={payloadDump}";
			Log.Error(message);
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"relic-option-choice {context}", message);
		}

		try
		{
			return ModelDb.GetById<RelicModel>(optionIds[selectedIndex]);
		}
		catch (Exception ex)
		{
			string message =
				$"[{ModInfo.Id}][RelicOptionChoice] Failed to load synced selected model: player={player.NetId} " +
				$"index={selectedIndex} context={context} id={optionIds[selectedIndex]}";
			Log.Error($"{message} error={ex}");
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"relic-option-choice {context}", message, ex);
		}
	}

}
