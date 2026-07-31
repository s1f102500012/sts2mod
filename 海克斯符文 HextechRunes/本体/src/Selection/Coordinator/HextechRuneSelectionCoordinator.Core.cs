using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Saves;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static readonly HextechActSelectionGate ActSelectionGate = new();

	public static void ResetActSelectionState()
	{
		ActSelectionGate.Reset();
	}

	public static Task HandleActStarted(HextechMayhemModifier modifier)
	{
		return HandleActSelection(modifier.ActiveRunState, modifier);
	}

	public static async Task HandleActSelection(RunState runState, HextechMayhemModifier modifier)
	{
		int actIndex = runState.CurrentActIndex;
		if (!modifier.IsActResolved(actIndex) && modifier.TryRecoverResolvedActsFromPlayerRelics(nameof(HandleActSelection)))
		{
			HextechEnemyUi.Refresh(modifier);
		}

		if (ActSelectionGate.ResetIfStaleRun(runState))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection: clearing stale handling state for previous run");
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection enter: room={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={actIndex} resolved={modifier.IsActResolved(actIndex)} handling={ActSelectionGate.IsHandling}");
		if (ActSelectionGate.IsHandling || !IsCurrentRun(runState) || actIndex < 0 || actIndex > 2 || modifier.IsActResolved(actIndex))
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection skip");
			return;
		}

		ActSelectionGate.Enter(runState);
		bool reopenMapAfterSelection = false;
		try
		{
			if (!await WaitForSelectionBlockingOverlaysToClear(runState, actIndex, "before-map-close"))
			{
				return;
			}

			if (NMapScreen.Instance?.IsOpen == true && NGame.Instance != null)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection: closing map before showing selection overlay");
				NMapScreen.Instance.Close(animateOut: false);
				reopenMapAfterSelection = true;
				await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			if (!IsCurrentRun(runState))
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: run is no longer current");
				return;
			}
			if (!await WaitForSelectionBlockingOverlaysToClear(runState, actIndex, "before-selection"))
			{
				return;
			}

			foreach (Player player in runState.Players)
			{
				RemoveRunesFromGrabBags(player);
			}

			(HextechRarityTier rarity, MonsterHexKind? monsterHex) = await ResolveActRoll(runState, modifier, actIndex);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection rarity: act={actIndex} rarity={rarity}");
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection monsterHex: act={actIndex} hex={monsterHex}");
			IReadOnlyList<MonsterHexKind> previousMonsterHexes = modifier.GetActiveMonsterHexesBeforeAct(actIndex);
			IReadOnlyList<MonsterHexKind> newMonsterHexes = ResolveNewMonsterHexesForAct(modifier, rarity, runState, actIndex, monsterHex);
			IReadOnlyList<MonsterHexKind> finalMonsterHexes = CombineMonsterHexes(previousMonsterHexes, newMonsterHexes);
			MonsterHexKind? visibleMonsterHex = FirstMonsterHexOrNull(newMonsterHexes);
			RelicModel? monsterHexRelic = CreateMonsterHexRelic(visibleMonsterHex);
			int playerHexCount = modifier.GetPlayerHexCountForAct(actIndex);

			// 模组总开关:在 act-roll(已完成两端握手/房主同步)之后冻结本局值。禁用则不发放任何玩家符文、
			// 不分配敌方海克斯——本局表现为原版。仍走到下方 SetMonsterHexesForAct(空)+SetActResolved(true) 正常收尾,
			// 两端对称、不破坏联机同步。
			if (modifier.FreezeModActiveForRunAndCheckDisabled())
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection: mod disabled for this run; vanilla act={actIndex} (no player runes / enemy hexes)");
				finalMonsterHexes = [];
				visibleMonsterHex = null;
				monsterHexRelic = null;
				playerHexCount = 0;
			}

			NetGameType gameType = RunManager.Instance.NetService.Type;
			for (int choiceOrdinal = 0; choiceOrdinal < playerHexCount; choiceOrdinal++)
			{
				bool allowEnemyHexAdjustment = choiceOrdinal == 0;
				if (gameType is NetGameType.Singleplayer or NetGameType.None)
				{
					foreach (Player player in runState.Players)
					{
						HashSet<ModelId> excludedIds = CreateBaseExcludedIds(modifier, player, finalMonsterHexes);
						List<RelicModel> options = BuildSelectableRunesForRarity(
							player,
							rarity,
							runState,
							excludedIds,
							useEndlessTagWindow: modifier.IsEndlessLoopActive);
						if (options.Count == 0)
						{
							Log.Warn($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection no options: player={player.NetId} act={actIndex} ordinal={choiceOrdinal} rarity={rarity}");
							continue;
						}

						HashSet<ModelId> enemyRerollExcludedIds = CreateEnemyHexRerollExcludedIds(options);
						HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection options: player={player.NetId} ordinal={choiceOrdinal} count={options.Count} ids={string.Join(",", options.Select(o => (o.CanonicalInstance?.Id ?? o.Id).Entry))}");
						// choiceOrdinal>0(!allowEnemyHexAdjustment):敌方 hex 已在第一次选择时定妥,后续玩家符文选择只读展示
						// 【本幕新增】的敌方 hex(newMonsterHexes 在首次选择后已更新为调整后的结果),不给控件。
						// 不能用 finalMonsterHexes:它含前几幕累积集,会把历史敌方海克斯一起显示(玩家实报)。
						HextechEnemyHexAdjustmentOptions? enemyHexOptions = finalMonsterHexes.Count > 0
							? new HextechEnemyHexAdjustmentOptions
							{
								InitialHexes = newMonsterHexes,
								ExcludedHexes = finalMonsterHexes,
								RerollLimit = modifier.MonsterHexRerollLimit,
								ControlsEnabled = allowEnemyHexAdjustment && newMonsterHexes.Count > 0,
								RerollFunc = allowEnemyHexAdjustment && newMonsterHexes.Count > 0
									? (currentHexes, slotIndex, rerollOrdinal) => RerollEnemyHexForAct(
										modifier,
										rarity,
										runState,
										actIndex,
										GetMonsterHexSlot(currentHexes, slotIndex),
										rerollOrdinal,
										CreateEnemyHexRerollExcludedIds(enemyRerollExcludedIds, currentHexes, slotIndex))
									: null
							}
							: null;
						RuneSelectionResult selection = await SelectRune(
							modifier,
							player,
							actIndex,
							choiceOrdinal,
							options,
							monsterHexRelic,
							enemyHexOptions);
						if (!IsCurrentRun(runState))
						{
							HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: selection returned for stale run");
							return;
						}

						if (allowEnemyHexAdjustment)
						{
							newMonsterHexes = selection.ResolvedMonsterHexes;
							finalMonsterHexes = CombineMonsterHexes(previousMonsterHexes, newMonsterHexes);
							visibleMonsterHex = FirstMonsterHexOrNull(newMonsterHexes);
							monsterHexRelic = CreateMonsterHexRelic(visibleMonsterHex);
						}

						RelicModel selected = RequireCompletedSelection(
							selection.SelectedRelic,
							$"singleplayer act={actIndex} ordinal={choiceOrdinal} player={player.NetId}");
						HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, player, selection.FinalOptions, selected, selection.RerollCount, choiceOrdinal);
						await RelicCmd.Obtain(selected, player);
						HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection obtained: player={player.NetId} ordinal={choiceOrdinal} relic={(selected.CanonicalInstance?.Id ?? selected.Id).Entry}");
					}
				}
				else
				{
					finalMonsterHexes = await SelectRunesForAllPlayersMultiplayer(
						runState,
						modifier,
						actIndex,
						rarity,
						allowEnemyHexAdjustment ? previousMonsterHexes : finalMonsterHexes,
						allowEnemyHexAdjustment ? newMonsterHexes : [],
						monsterHexRelic,
						choiceOrdinal);
					if (allowEnemyHexAdjustment)
					{
						newMonsterHexes = finalMonsterHexes
							.Where(hex => !previousMonsterHexes.Contains(hex))
							.ToArray();
						visibleMonsterHex = FirstMonsterHexOrNull(newMonsterHexes);
						monsterHexRelic = CreateMonsterHexRelic(visibleMonsterHex);
					}
				}
			}

			if (playerHexCount <= 0)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection skipped player choices: act={actIndex} configuredPlayerHexCount={playerHexCount}");
			}
			if (!IsCurrentRun(runState))
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: run changed before resolving act");
				return;
			}

			modifier.SetMonsterHexesForAct(actIndex, finalMonsterHexes);
			modifier.SetActResolved(actIndex, true);
			modifier.ApplyMapModifiersToCurrentAct(nameof(HandleActSelection));
			HextechEnemyUi.Refresh(modifier);
			await modifier.ApplyToCurrentEnemiesIfNeeded();
			await PersistActSelection(runState, actIndex);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection resolved: act={actIndex}");
		}
		catch (OperationCanceledException)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: selection overlay closed before choice act={actIndex}");
		}
		catch (Exception ex)
		{
			// R2:任何非取消异常都不得冒泡出 HandleActSelection。否则会 fault 掉把本方法 await 进去的
			// 开局/进幕任务链(StartRun 续体、AfterActEntered/BeforeRoomEntered 等 lockstep 钩子),
			// 造成「单端被踢出/任务挂起、另一端继续」式分叉。此处捕获后:抛点通常早于 SetActResolved,
			// 故该幕仍为未解析,后续 room-entered/load 会经 ActSelectionGate 重入重试(两端对称、可自愈);
			// 若确属两端内容/资源不一致的真分叉,会在战斗开始时由游戏自带 NetFullCombatState checksum
			// (StateDivergence)统一、干净地断连——不在此自造断连,避免对可自愈的瞬时/对称失败过度踢人。
			// 用 Log.Error 保证可诊断,绝不静默吞掉。
			Log.Error($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection failed act={actIndex} networkMp={HextechRelicBase.IsNetworkMultiplayerRun()}: {ex}");
		}
		finally
		{
			if (reopenMapAfterSelection
				&& IsCurrentRun(runState)
				&& NMapScreen.Instance != null
				&& !NMapScreen.Instance.IsOpen)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection: reopening map after selection overlay");
				NMapScreen.Instance.Open();
			}

			ActSelectionGate.ExitIfCurrent(runState);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection exit: act={actIndex}");
		}
	}

	private static async Task<bool> WaitForSelectionBlockingOverlaysToClear(RunState runState, int actIndex, string reason)
	{
		for (int frame = 0; IsCurrentRun(runState); frame++)
		{
			object? topOverlay = NOverlayStack.Instance?.Peek();
			if (!IsSelectionBlockingOverlay(topOverlay, out string overlayName))
			{
				return true;
			}

			if (frame % 120 == 0)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection waiting: act={actIndex} reason={reason} topOverlay={overlayName} frame={frame}");
			}

			await WaitOneFrame();
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: run changed while waiting for overlays act={actIndex} reason={reason}");
		return false;
	}

	private static bool IsSelectionBlockingOverlay(object? overlay, out string overlayName)
	{
		if (overlay == null || overlay is HextechRuneSelectionScreen)
		{
			overlayName = overlay?.GetType().FullName ?? "null";
			return false;
		}

		Type overlayType = overlay.GetType();
		overlayName = overlayType.FullName ?? overlayType.Name;
		if (overlay is IOverlayScreen { ScreenType: NetScreenType.Rewards })
		{
			return true;
		}

		return overlayName.Contains("Reward", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task WaitOneFrame()
	{
		if (NGame.Instance != null)
		{
			await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
			return;
		}

		await Task.Yield();
	}

	private static async Task PersistActSelection(RunState runState, int actIndex)
	{
		try
		{
			if (!IsCurrentRun(runState) || RunManager.Instance.NetService.Type == NetGameType.Replay)
			{
				return;
			}

			await SaveManager.Instance.SaveRun(null!, saveProgress: false);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] PersistActSelection: saved current run after resolving act={actIndex}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] PersistActSelection failed: act={actIndex} error={ex}");
		}
	}
}
