using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
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
		// RebuildCards 会在当前输入事件内销毁并重建按钮。重新开启确认保护，避免鼠标、
		// 手柄确认键或键盘重复输入落到新生成的卡片上，表现为“刷新后直接跳过”。
		RestartSelectionConfirmGuard();
		RebuildCards();
	}

	private void OnEnemyHexRerollPressed(int slotIndex)
	{
		if (_choiceLocked || _enemyHexRerollFunc == null || slotIndex < 0 || slotIndex >= _monsterHexKinds.Count || IsEnemyHexRerollLimitReached(slotIndex))
		{
			return;
		}

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

		_monsterHexKinds[slotIndex] = rerolled;
		_enemyHexRerollCounts[slotIndex]++;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnEnemyHexRerollPressed: slot={slotIndex} hex={rerolled} count={_enemyHexRerollCounts[slotIndex]}");
		NotifyEnemyHexChanged();
		RebuildEnemyPreview();
	}

	private void OnEnemyHexRemovePressed(int slotIndex)
	{
		if (_choiceLocked || slotIndex < 0 || slotIndex >= _monsterHexKinds.Count)
		{
			return;
		}

		if (!_monsterHexKinds[slotIndex].HasValue)
		{
			_monsterHexKinds[slotIndex] = _monsterHexBeforeRemoval[slotIndex];
			_monsterHexBeforeRemoval[slotIndex] = null;
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnEnemyHexRemovePressed: undo slot={slotIndex} hex={_monsterHexKinds[slotIndex]}");
		}
		else
		{
			_monsterHexBeforeRemoval[slotIndex] = _monsterHexKinds[slotIndex];
			_monsterHexKinds[slotIndex] = null;
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnEnemyHexRemovePressed: remove slot={slotIndex} previous={_monsterHexBeforeRemoval[slotIndex]}");
		}

		NotifyEnemyHexChanged();
		RebuildEnemyPreview();
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

		await WaitForMouseReleaseAsync();
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.DismissAfterSelectionComplete: removing overlay");
		_blockMapUntilDismissed = false;
		NOverlayStack.Instance?.Remove(this);
	}

	private async Task WaitForMouseReleaseAsync()
	{
		if (!await AwaitProcessFrameIfInsideTreeAsync())
		{
			return;
		}

		while (Input.IsMouseButtonPressed(MouseButton.Left))
		{
			if (!await AwaitProcessFrameIfInsideTreeAsync())
			{
				return;
			}
		}
		await AwaitProcessFrameIfInsideTreeAsync();
	}

	private async Task<bool> AwaitProcessFrameIfInsideTreeAsync()
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

		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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

	public void AfterOverlayOpened()
	{
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayOpened");
		EnsureSelectionConfirmGuardStarted();
		EnsureMapButtonEnabled();
		Modulate = Colors.White;
		Visible = true;
		TryGrabOverlayFocus();
	}

	// 第二/三层的海克斯选择发生在「刚进新幕、还没进房间」时，顶栏地图键被游戏置灰禁用，
	// 玩家点不开地图。选择期间临时点亮它，让上面的只读地图预览能被触发；选择结束时还原。
	private void EnsureMapButtonEnabled()
	{
		if (_closed)
		{
			return;
		}

		NTopBarMapButton? mapButton = GetTopBarMapButton();
		if (mapButton == null || mapButton.IsEnabled)
		{
			return;
		}

		_mapButtonForceEnabled = true;
		mapButton.Enable();
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen: temporarily enabled top bar map button for selection map preview");
	}

	private void RestoreMapButtonState()
	{
		if (!_mapButtonForceEnabled)
		{
			return;
		}

		_mapButtonForceEnabled = false;
		GetTopBarMapButton()?.Disable();
	}

	private static NTopBarMapButton? GetTopBarMapButton()
	{
		try
		{
			return NRun.Instance?.GlobalUi?.TopBar?.Map;
		}
		catch
		{
			return null;
		}
	}

	public void AfterOverlayClosed()
	{
		if (_closed)
		{
			return;
		}

		_closed = true;
		_blockMapUntilDismissed = false;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayClosed");
		if (!_choiceLocked)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayClosed: cancelling unresolved selection");
			_completionSource.TrySetCanceled();
		}

		QueueFree();
	}

	public void AfterOverlayShown()
	{
		EnsureSelectionConfirmGuardStarted();
		// 地图已关、overlay 已被重新显示：清理预览态（恢复行进/移除提示），不重复 ShowOverlays。
		EndMapPreview(restoreOverlay: false);
		EnsureMapButtonEnabled();
		Visible = true;
		TryGrabOverlayFocus();
	}

	public void AfterOverlayHidden()
	{
		if (_closed)
		{
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayHidden: choiceLocked={_choiceLocked} capstoneOpen={NCapstoneContainer.Instance?.InUse == true} mapOpen={NMapScreen.Instance?.IsOpen == true}");
		Visible = false;

		// 选择尚未完成、且玩家打开了地图 → 进入只读地图预览：禁止行进、显示提示，保留地图打开。
		// 不强制关闭地图、不触碰选择的 TaskCompletionSource，所以查看地图绝不会跳过海克斯选择。
		if (!_choiceLocked && !_blockMapUntilDismissed && IsInsideTree() && NMapScreen.Instance?.IsOpen == true)
		{
			BeginMapPreview();
			return;
		}

		if ((!_choiceLocked || _blockMapUntilDismissed) && !_restoreAfterMapReopenQueued && IsInsideTree())
		{
			_restoreAfterMapReopenQueued = true;
			_ = TaskHelper.RunSafely(RestoreAfterMapReopenAsync());
		}
	}

	private void BeginMapPreview()
	{
		if (_mapPreviewActive)
		{
			return;
		}

		NMapScreen? map = NMapScreen.Instance;
		if (map == null)
		{
			return;
		}

		_mapPreviewActive = true;
		map.SetTravelEnabled(enabled: false);   // 只读：禁止在地图上选节点前进，杜绝靠地图跳过海克斯选择
		map.Closed += OnMapPreviewClosed;        // 玩家关闭地图后把选择界面恢复回来
		ShowMapPreviewHint(map);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.BeginMapPreview: read-only map preview, selection still pending");
	}

	private void OnMapPreviewClosed()
	{
		EndMapPreview(restoreOverlay: true);
	}

	private void EndMapPreview(bool restoreOverlay)
	{
		if (!_mapPreviewActive)
		{
			return;
		}

		_mapPreviewActive = false;
		NMapScreen? map = NMapScreen.Instance;
		if (map != null)
		{
			map.Closed -= OnMapPreviewClosed;
			map.SetTravelEnabled(enabled: true);  // 恢复地图行进可用性，供选完后正常选路（地图此刻已关，恢复无即时副作用）
		}

		HideMapPreviewHint();

		if (!restoreOverlay || _closed || !IsInsideTree())
		{
			return;
		}

		NOverlayStack.Instance?.ShowOverlays();   // 把选择界面重新顶回来
		Visible = true;
		TryGrabOverlayFocus();
	}

	private void ShowMapPreviewHint(NMapScreen map)
	{
		if (_mapPreviewHint == null || !GodotObject.IsInstanceValid(_mapPreviewHint))
		{
			_mapPreviewHint = BuildMapPreviewHint();
		}

		if (_mapPreviewHint.GetParent() == null)
		{
			map.AddChild(_mapPreviewHint);
		}

		_mapPreviewHint.Visible = true;
	}

	private void HideMapPreviewHint()
	{
		if (_mapPreviewHint != null && GodotObject.IsInstanceValid(_mapPreviewHint))
		{
			_mapPreviewHint.Visible = false;
		}
	}

	private MegaLabel BuildMapPreviewHint()
	{
		MegaLabel hint = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MaxFontSize = 26,
			MinFontSize = 18,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 4096
		};
		ApplyDefaultMegaLabelTheme(hint);
		hint.SetAnchorsPreset(LayoutPreset.BottomWide);
		hint.OffsetTop = -96f;
		hint.OffsetBottom = -44f;
		hint.SetTextAutoSize(new LocString(LocTable, "HEXTECH_MAP_PREVIEW_HINT").GetRawText());
		hint.Modulate = new Color(1f, 0.93f, 0.7f, 0.96f);
		return hint;
	}

	private async Task RestoreAfterMapReopenAsync()
	{
		try
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInsideTree() || (_choiceLocked && !_blockMapUntilDismissed))
			{
				return;
			}

			bool isTopOverlay = ReferenceEquals(NOverlayStack.Instance?.Peek(), this);
			bool capstoneOpen = NCapstoneContainer.Instance?.InUse == true;
			bool mapOpen = NMapScreen.Instance?.IsOpen == true;
			if (!isTopOverlay || capstoneOpen || !mapOpen)
			{
				return;
			}

			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RestoreAfterMapReopen: closing map reopened over blocking selection");
			NMapScreen.Instance?.Close(animateOut: false);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			NOverlayStack.Instance?.ShowOverlays();
		}
		finally
		{
			_restoreAfterMapReopenQueued = false;
		}
	}

	private void TryGrabOverlayFocus()
	{
		if (_closed || !IsInsideTree() || !IsVisibleInTree() || FocusMode == FocusModeEnum.None)
		{
			return;
		}

		GrabFocus();
	}
}
