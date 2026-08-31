using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private const int DismissMouseReleaseWaitLimit = 30;

	private void OnHolderSelected(RelicModel relic)
	{
		if (_choiceLocked)
		{
			return;
		}

		if (IsSelectionConfirmGuardActive())
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnHolderSelected: ignored early selection relic={(relic.CanonicalInstance?.Id ?? relic.Id).Entry}");
			GetViewport()?.SetInputAsHandled();
			return;
		}

		_choiceLocked = true;
		foreach (Button holder in _holders)
		{
			holder.Disabled = true;
		}
		foreach (Button rerollButton in _rerollButtons)
		{
			rerollButton.Disabled = true;
		}
		foreach (HextechGoldenRerollVisual visual in _goldenRerollVisuals)
		{
			visual.SetVisualState(active: false, hovered: false, disabled: true);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnHolderSelected: relic={(relic.CanonicalInstance?.Id ?? relic.Id).Entry}");
		PlayRuneSelectSfx(relic);
		GetViewport()?.SetInputAsHandled();
		_completionSource.TrySetResult([relic]);
	}

	private void EnsureSelectionConfirmGuardStarted()
	{
		if (_selectionConfirmGuardStarted)
		{
			return;
		}

		RestartSelectionConfirmGuard();
	}

	private void RestartSelectionConfirmGuard()
	{
		_selectionConfirmGuardStarted = true;
		_selectionConfirmGuardEndsAtMsec = Time.GetTicksMsec() + SelectionConfirmGuardDurationMsec;
	}

	private bool IsSelectionConfirmGuardActive()
	{
		EnsureSelectionConfirmGuardStarted();
		return Time.GetTicksMsec() < _selectionConfirmGuardEndsAtMsec;
	}

	private void OnRerollPressed(int slotIndex)
	{
		if (_choiceLocked || _rerollFunc == null || IsPlayerRuneRerollLimitReached(slotIndex))
		{
			return;
		}

		bool restoreControllerFocus = slotIndex >= 0
			&& slotIndex < _rerollButtons.Count
			&& _rerollButtons[slotIndex].HasFocus();
		bool goldenRerollWasActive = _goldenRerollSession?.IsActive == true;
		IReadOnlyList<RelicModel> rerolled = _rerollFunc(_relics, slotIndex, _rerollHistory.Count);
		if (rerolled.Count != _relics.Count)
		{
			return;
		}

		string oldRelic = (_relics[slotIndex].CanonicalInstance?.Id ?? _relics[slotIndex].Id).Entry;
		string newRelic = (rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id).Entry;
		if (oldRelic == newRelic)
		{
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnRerollPressed: slot={slotIndex} old={oldRelic} new={newRelic}");
		PlayRerollSfx();
		_relics = rerolled.ToList();
		_playerRuneRerollCounts[slotIndex]++;
		_rerollHistory.Add(slotIndex);
		if (goldenRerollWasActive)
		{
			_goldenRerollSession!.Consume();
			HextechLog.Info(
				$"[{ModInfo.Id}][Mayhem] SelectionScreen.OnRerollPressed: golden reroll consumed " +
				$"slot={slotIndex} upgraded={_goldenRerollSession.UpgradedRarity}");
		}
		// RebuildCards 会在当前输入事件内销毁并重建按钮。重新开启确认保护，避免鼠标、
		// 手柄确认键或键盘重复输入落到新生成的卡片上，表现为“刷新后直接跳过”。
		RestartSelectionConfirmGuard();
		RebuildCards();
		if (restoreControllerFocus)
		{
			RestorePlayerRerollFocus(slotIndex);
		}
	}

	internal bool ActivateGoldenRerollForDebug()
	{
		if (_choiceLocked || _goldenRerollSession?.ActivateForDebug() != true)
		{
			return false;
		}

		for (int i = 0; i < _goldenRerollVisuals.Count; i++)
		{
			bool disabled = i >= _rerollButtons.Count || _rerollButtons[i].Disabled;
			_goldenRerollVisuals[i].StartAnimationLoop();
			_goldenRerollVisuals[i].SetVisualState(
				active: true,
				hovered: false,
				disabled);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen: golden reroll forced by console");
		return true;
	}

	private void OnEnemyHexRerollPressed(int slotIndex)
	{
		if (_choiceLocked || _enemyHexRerollFunc == null || slotIndex < 0 || slotIndex >= _monsterHexKinds.Count || IsEnemyHexRerollLimitReached(slotIndex))
		{
			return;
		}

		bool restoreControllerFocus = slotIndex < _enemyHexRerollButtons.Count
			&& _enemyHexRerollButtons[slotIndex].HasFocus();
		MonsterHexKind? currentHex = _monsterHexKinds[slotIndex];
		if (!currentHex.HasValue)
		{
			return;
		}

		MonsterHexKind? rerolled = _enemyHexRerollFunc(_monsterHexKinds.ToArray(), slotIndex, _enemyHexRerollCounts[slotIndex]);
		if (rerolled == null || rerolled == currentHex)
		{
			return;
		}

		PlayRerollSfx();
		_monsterHexKinds[slotIndex] = rerolled;
		_enemyHexRerollCounts[slotIndex]++;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnEnemyHexRerollPressed: slot={slotIndex} hex={rerolled} count={_enemyHexRerollCounts[slotIndex]}");
		NotifyEnemyHexChanged();
		RebuildEnemyPreview();
		if (restoreControllerFocus)
		{
			RestoreEnemyRerollFocus(slotIndex);
		}
	}

	private void OnEnemyHexRemovePressed(int slotIndex)
	{
		if (_choiceLocked || slotIndex < 0 || slotIndex >= _monsterHexKinds.Count)
		{
			return;
		}

		bool restoreControllerFocus = slotIndex < _enemyHexRemoveButtons.Count
			&& _enemyHexRemoveButtons[slotIndex].HasFocus();
		bool wasRemoved = !_monsterHexKinds[slotIndex].HasValue;
		MonsterHexKind? previous = wasRemoved
			? GetMonsterHexBeforeRemovalSlot(slotIndex)
			: _monsterHexKinds[slotIndex];
		if (!ToggleEnemyHexRemoval(_monsterHexKinds, _monsterHexBeforeRemoval, slotIndex))
		{
			return;
		}

		PlayButtonClickSfx();
		if (wasRemoved)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnEnemyHexRemovePressed: undo slot={slotIndex} hex={previous}");
		}
		else
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnEnemyHexRemovePressed: remove slot={slotIndex} previous={previous}");
		}

		NotifyEnemyHexChanged();
		RebuildEnemyPreview();
		if (restoreControllerFocus)
		{
			RestoreEnemyRemoveFocus(slotIndex);
		}
	}

	internal static bool ToggleEnemyHexRemoval(
		IList<MonsterHexKind?> monsterHexes,
		IList<MonsterHexKind?> monsterHexesBeforeRemoval,
		int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= monsterHexes.Count || slotIndex >= monsterHexesBeforeRemoval.Count)
		{
			return false;
		}

		if (monsterHexes[slotIndex].HasValue)
		{
			monsterHexesBeforeRemoval[slotIndex] = monsterHexes[slotIndex];
			monsterHexes[slotIndex] = null;
			return true;
		}

		if (!monsterHexesBeforeRemoval[slotIndex].HasValue)
		{
			return false;
		}

		monsterHexes[slotIndex] = monsterHexesBeforeRemoval[slotIndex];
		monsterHexesBeforeRemoval[slotIndex] = null;
		return true;
	}

	public void ApplyEnemyHexAdjustment(MonsterHexKind? monsterHex, bool removed, int rerollCount)
	{
		ApplyEnemyHexAdjustment([ removed ? null : monsterHex ], [ rerollCount ]);
	}

	public void ApplyEnemyHexAdjustment(IReadOnlyList<MonsterHexKind?> monsterHexes, IReadOnlyList<int> rerollCounts)
	{
		_monsterHexKinds.Clear();
		_monsterHexBeforeRemoval.Clear();
		_enemyHexRerollCounts.Clear();
		for (int i = 0; i < monsterHexes.Count; i++)
		{
			_monsterHexKinds.Add(monsterHexes[i]);
			_monsterHexBeforeRemoval.Add(null);
			_enemyHexRerollCounts.Add(i < rerollCounts.Count ? Math.Max(0, rerollCounts[i]) : 0);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.ApplyEnemyHexAdjustment: slots={string.Join(",", _monsterHexKinds.Select(static hex => hex?.ToString() ?? "None"))} rerolls={string.Join(",", _enemyHexRerollCounts)}");
		RebuildEnemyPreview();
	}

	private void NotifyEnemyHexChanged()
	{
		_enemyHexChanged?.Invoke(_monsterHexKinds.ToArray(), _enemyHexRerollCounts.ToArray());
	}

	private bool IsPlayerRuneRerollLimitReached(int slotIndex)
	{
		return IsRerollLimitReached(_playerRuneRerollLimit, GetPlayerRuneRerollCount(slotIndex));
	}

	private int GetPlayerRuneRerollCount(int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < _playerRuneRerollCounts.Count
			? _playerRuneRerollCounts[slotIndex]
			: 0;
	}

	private bool IsEnemyHexRerollLimitReached(int slotIndex)
	{
		int count = slotIndex >= 0 && slotIndex < _enemyHexRerollCounts.Count
			? _enemyHexRerollCounts[slotIndex]
			: 0;
		return IsRerollLimitReached(_enemyHexRerollLimit, count);
	}

	private static bool IsRerollLimitReached(int limit, int count)
	{
		return limit != HextechRuneConfiguration.InfiniteRerollLimit && count >= limit;
	}

	public async Task<IEnumerable<RelicModel>> RelicsSelected(bool removeOverlay = true)
	{
		IEnumerable<RelicModel> result = await _completionSource.Task;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RelicsSelected: begin dismiss mousePressed={Input.IsMouseButtonPressed(MouseButton.Left)}");
		await WaitForMouseReleaseAsync();
		if (!removeOverlay)
		{
			_blockMapUntilDismissed = true;
			ShowWaitingForRemotePlayers();
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RelicsSelected: keeping overlay until multiplayer sync completes");
			return result;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RelicsSelected: removing overlay");
		NOverlayStack.Instance?.Remove(this);
		return result;
	}

	public async Task DismissAfterSelectionComplete()
	{
		if (!IsInsideTree())
		{
			return;
		}

		bool mouseReleased = await WaitForMouseReleaseAsync(DismissMouseReleaseWaitLimit);
		if (!mouseReleased)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.DismissAfterSelectionComplete: mouse release wait reached its limit; forcing overlay removal.");
		}
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.DismissAfterSelectionComplete: removing overlay");
		_blockMapUntilDismissed = false;
		NOverlayStack.Instance?.Remove(this);
	}

	private async Task<bool> WaitForMouseReleaseAsync(
		int pressedWaitLimit = int.MaxValue,
		CancellationToken cancellationToken = default)
	{
		if (!await AwaitProcessFrameIfInsideTreeAsync(cancellationToken))
		{
			return true;
		}

		int pressedWaitCount = 0;
		while (Input.IsMouseButtonPressed(MouseButton.Left))
		{
			if (pressedWaitCount >= pressedWaitLimit)
			{
				return false;
			}

			pressedWaitCount++;
			if (!await AwaitProcessFrameIfInsideTreeAsync(cancellationToken))
			{
				return true;
			}
		}

		await AwaitProcessFrameIfInsideTreeAsync(cancellationToken);
		return true;
	}

	private async Task<bool> AwaitProcessFrameIfInsideTreeAsync(CancellationToken cancellationToken = default)
	{
		if (!IsInsideTree())
		{
			return false;
		}

		SceneTree tree = GetTree();
		if (tree == null)
		{
			return false;
		}

		await HextechSelectionHelpers.WaitForProcessFrameOrDelayAsync(cancellationToken);
		return IsInsideTree();
	}

	private void ShowWaitingForRemotePlayers()
	{
		if (_statusLabel == null)
		{
			return;
		}

		_statusLabel.SetTextAutoSize(new LocString(LocTable, "HEXTECH_WAITING_FOR_PLAYERS").GetRawText());
		_statusLabel.Visible = true;
	}
}
