using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static class HextechForgeSelectionCoordinator
{
	private const string LocTable = "relic_collection";

	public static async Task<RelicModel?> SelectForge(Player player, IReadOnlyList<RelicModel> options, string context, bool syncMultiplayerChoice = true)
	{
		if (options.Count > HextechStableModelIdListCodec.MaxCount)
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				options.Count,
				$"Forge option count must not exceed {HextechStableModelIdListCodec.MaxCount}.");
		}

		if (options.Count == 0)
		{
			Log.Warn($"[{ModInfo.Id}][ForgeChoice] No forge options available: player={player.NetId} context={context}");
			return null;
		}

		MarkRelicsSeen(options);

		// 杂项配置「随机获得锻造器」开启时,跳过三选一界面,直接从同一候选池稳定随机给一个。
		// 用 HextechStableRandom 基于 RunState 种子决定,所有客户端独立算出同一结果;reward 路径
		// 又只在选择方执行并经 RewardSynchronizer 广播,因此无需 PlayerChoiceSynchronizer 往返,联机一致。
		// 配置经 RunConfigurationSnapshot 跟随主机,故双端要么都短路要么都不短路。
		if (ShouldDirectlyGrantRandomForge(player))
		{
			RelicModel directGrant = PickStableRandomForge(player, options, context);
			HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Random direct grant (choice skipped): player={player.NetId} relic={(directGrant.CanonicalInstance?.Id ?? directGrant.Id).Entry} context={context}");
			return directGrant;
		}

		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			return await SelectLocalForge(player, options, context);
		}

		if (!syncMultiplayerChoice)
		{
			if (HextechRuneSelectionCoordinator.IsLocalPlayer(runManager, player))
			{
				return await SelectLocalForge(player, options, context);
			}

			Log.Warn($"[{ModInfo.Id}][ForgeChoice] Unsynced forge selection ignored for remote player={player.NetId} context={context}");
			return null;
		}

		PlayerChoiceSynchronizer synchronizer = await HextechRuneSelectionCoordinator.WaitForPlayerChoiceSynchronizerAsync(runManager);

		uint choiceId = synchronizer.ReserveChoiceId(player);
		int operationToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			choiceId,
			player.NetId,
			context);
		if (HextechRuneSelectionCoordinator.IsLocalPlayer(runManager, player))
		{
			try
			{
				RelicModel? selected = await SelectLocalForge(player, options, context);
				if (selected == null)
				{
					uint canceledChoiceId = HextechRuneSelectionCoordinator.SyncLocalHextechChoice(
						synchronizer,
						player,
						choiceId,
						HextechChoiceCodec.CreateForgeSelection(operationToken, selectedIndex: -1, options),
						$"forge-choice {context}");
					HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Local selection canceled: player={player.NetId} choiceId={canceledChoiceId} context={context}");
					return null;
				}

				int selectedIndex = IndexOfRelicById(options, selected);
				if (selectedIndex < 0)
				{
					string message = $"Local forge selection is not in the synchronized option set: player={player.NetId} context={context}";
					throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"forge-choice {context}", message);
				}

				if (!runManager.NetService.IsConnected)
				{
					throw new OperationCanceledException(
						$"Local forge selection ended after multiplayer disconnected: player={player.NetId} context={context}");
				}

				uint sentChoiceId = HextechRuneSelectionCoordinator.SyncLocalHextechChoice(
					synchronizer,
					player,
					choiceId,
					HextechChoiceCodec.CreateForgeSelection(operationToken, selectedIndex, options),
					$"forge-choice {context}");

				HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Sync local: player={player.NetId} choiceId={sentChoiceId} index={selectedIndex} context={context}");
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
					$"Local forge transaction failed after reserving choice: " +
					$"player={player.NetId} choiceId={choiceId} context={context}";
				throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"forge-choice {context}", message, ex);
			}
		}

		HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Wait remote: player={player.NetId} choiceId={choiceId} context={context}");
		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = await HextechRuneSelectionCoordinator.WaitForRemoteHextechChoice(
			synchronizer,
			(RunState)player.RunState,
			player,
			choiceId,
			choice => HextechChoiceCodec.IsForgeSelection(choice, operationToken, options),
			$"forge-choice {context}");
		HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Remote received: player={player.NetId} choiceId={receivedChoiceId} context={context}");
		return ResolveRemoteForgeChoice(player, options, remoteChoice, operationToken, context);
	}

	private static async Task<RelicModel?> SelectLocalForge(Player player, IReadOnlyList<RelicModel> options, string context)
	{
		try
		{
			HextechRuneSelectionScreen screen = await CreateForgeSelectionScreenAsync(options);
			RelicModel? selected = (await screen.RelicsSelected()).FirstOrDefault();
			HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Local selected: player={player.NetId} relic={(selected?.CanonicalInstance?.Id ?? selected?.Id)?.Entry ?? "null"} context={context}");
			return selected;
		}
		catch (OperationCanceledException)
		{
			HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Selection cancelled: player={player.NetId} context={context}");
			return null;
		}
	}

	private static async Task<HextechRuneSelectionScreen> CreateForgeSelectionScreenAsync(IReadOnlyList<RelicModel> options)
	{
		await WaitForSingletonAsync(static () => NOverlayStack.Instance);
		HextechRuneSelectionScreen screen = HextechRuneSelectionScreen.Create(
			options,
			monsterHexRelic: null,
			rerollFunc: null,
			enemyHexOptions: null,
			titleOverride: new LocString(LocTable, "HEXTECH_FORGE_SELECTION_TITLE").GetRawText(),
			metadataMode: HextechSelectionMetadataMode.Forge);
		if (NOverlayStack.Instance == null)
		{
			throw new InvalidOperationException("NOverlayStack is not available for forge selection.");
		}

		NOverlayStack.Instance.Push(screen);
		return screen;
	}

	private static RelicModel? ResolveRemoteForgeChoice(
		Player player,
		IReadOnlyList<RelicModel> expectedOptions,
		PlayerChoiceResult remoteChoice,
		int expectedOperationToken,
		string context)
	{
		string payloadDump = HextechChoiceCodec.TryGetIndexPayload(remoteChoice, out List<int> payload)
			? $"[{string.Join(",", payload)}]"
			: remoteChoice.ToString();
		if (!HextechChoiceCodec.TryDecodeForgeSelection(
			remoteChoice,
			expectedOperationToken,
			out int selectedIndex,
			out List<ModelId> optionIds))
		{
			string message = $"[{ModInfo.Id}][ForgeChoice] Malformed payload: player={player.NetId} context={context} payload={payloadDump}";
			Log.Error(message);
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"forge-choice {context}", message);
		}

		if (selectedIndex == -1)
		{
			HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Remote selection canceled: player={player.NetId} context={context}");
			return null;
		}

		if (optionIds.Count != expectedOptions.Count)
		{
			string message =
				$"[{ModInfo.Id}][ForgeChoice] Synced option count mismatch: player={player.NetId} " +
				$"expected={expectedOptions.Count} actual={optionIds.Count} context={context} payload={payloadDump}";
			Log.Error(message);
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"forge-choice {context}", message);
		}

		if (selectedIndex < 0 || selectedIndex >= optionIds.Count)
		{
			string message = $"[{ModInfo.Id}][ForgeChoice] Invalid selected index: player={player.NetId} index={selectedIndex} count={optionIds.Count} context={context} payload={payloadDump}";
			Log.Error(message);
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"forge-choice {context}", message);
		}

		try
		{
			return ModelDb.GetById<RelicModel>(optionIds[selectedIndex]).ToMutable();
		}
		catch (Exception ex)
		{
			string message =
				$"[{ModInfo.Id}][ForgeChoice] Failed to load synced selected model: player={player.NetId} " +
				$"index={selectedIndex} context={context} id={optionIds[selectedIndex]}";
			Log.Error($"{message} error={ex}");
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"forge-choice {context}", message, ex);
		}
	}

	private static bool ShouldDirectlyGrantRandomForge(Player player)
	{
		try
		{
			if (player.RunState is RunState runState
				&& runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() is HextechMayhemModifier modifier)
			{
				return modifier.RandomForgeDirectGrant;
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][ForgeChoice] Failed to read synchronized random-forge setting; using deterministic false fallback: player={player.NetId} error={ex}");
			return false;
		}

		return HextechRuneConfiguration.GetSnapshot().RandomForgeDirectGrant;
	}

	private static RelicModel PickStableRandomForge(Player player, IReadOnlyList<RelicModel> options, string context)
	{
		int index = HextechStableRandom.Index(
			(RunState)player.RunState,
			options.Count,
			"forge-direct-grant",
			HextechStableRandom.PlayerKey(player),
			context,
			player.Relics.Count.ToString());
		return options[Math.Clamp(index, 0, options.Count - 1)];
	}

}
