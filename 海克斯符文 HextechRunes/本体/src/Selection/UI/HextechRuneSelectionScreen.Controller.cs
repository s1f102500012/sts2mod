using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	public override void _Ready()
	{
		base._Ready();
		ConfigureControllerNavigation();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (_closed || !IsVisibleInTree())
		{
			return;
		}

		if (!_controllerNavigationActivated && HextechControllerInput.IsIntentional(inputEvent))
		{
			_controllerNavigationActivated = true;
			ConfigureControllerNavigation();
			Control? initialFocus = _holders.FirstOrDefault();
			RestoreFocusDeferred(initialFocus ?? this);
			GetViewport()?.SetInputAsHandled();
			return;
		}

		if (!inputEvent.IsActionPressed("ui_cancel"))
		{
			return;
		}

		// 海克斯选择是强制完成的 overlay；返回键只能收回输入焦点，不能绕过选择或关闭界面。
		GetViewport()?.SetInputAsHandled();
		TryGrabOverlayFocus();
	}

	private void ConfigureControllerNavigation()
	{
		if (!IsInsideTree())
		{
			return;
		}

		List<Control> cards = _holders.Cast<Control>().Where(CanReceiveFocus).ToList();
		List<Control> rerolls = _rerollButtons.Cast<Control>().Where(CanReceiveFocus).ToList();
		List<Control> enemyActions = [];
		int enemySlotCount = Math.Max(_enemyHexRerollButtons.Count, _enemyHexRemoveButtons.Count);
		for (int i = 0; i < enemySlotCount; i++)
		{
			if (i < _enemyHexRerollButtons.Count && CanReceiveFocus(_enemyHexRerollButtons[i]))
			{
				enemyActions.Add(_enemyHexRerollButtons[i]);
			}
			if (i < _enemyHexRemoveButtons.Count && CanReceiveFocus(_enemyHexRemoveButtons[i]))
			{
				enemyActions.Add(_enemyHexRemoveButtons[i]);
			}
		}

		ConfigureHorizontalNeighbors(cards);
		ConfigureHorizontalNeighbors(rerolls);
		ConfigureHorizontalNeighbors(enemyActions);

		for (int i = 0; i < _holders.Count; i++)
		{
			Button card = _holders[i];
			if (!CanReceiveFocus(card))
			{
				continue;
			}

			Control up = enemyActions.Count > 0
				? enemyActions[Math.Min(i, enemyActions.Count - 1)]
				: card;
			Control down = i < _rerollButtons.Count && CanReceiveFocus(_rerollButtons[i])
				? _rerollButtons[i]
				: card;
			card.FocusNeighborTop = up.GetPath();
			card.FocusNeighborBottom = down.GetPath();
		}

		for (int i = 0; i < _rerollButtons.Count; i++)
		{
			Button reroll = _rerollButtons[i];
			if (!CanReceiveFocus(reroll))
			{
				continue;
			}

			Control card = _holders[Math.Min(i, _holders.Count - 1)];
			reroll.FocusNeighborTop = card.GetPath();
			reroll.FocusNeighborBottom = reroll.GetPath();
		}

		for (int i = 0; i < enemyActions.Count; i++)
		{
			Control action = enemyActions[i];
			Control down = cards.Count > 0
				? cards[Math.Min(i, cards.Count - 1)]
				: action;
			action.FocusNeighborTop = action.GetPath();
			action.FocusNeighborBottom = down.GetPath();
		}
	}

	private static void ConfigureHorizontalNeighbors(IReadOnlyList<Control> controls)
	{
		for (int i = 0; i < controls.Count; i++)
		{
			Control current = controls[i];
			current.FocusNeighborLeft = controls[Math.Max(0, i - 1)].GetPath();
			current.FocusNeighborRight = controls[Math.Min(controls.Count - 1, i + 1)].GetPath();
		}
	}

	private static bool CanReceiveFocus(Control control)
	{
		return GodotObject.IsInstanceValid(control)
			&& control.IsInsideTree()
			&& control.Visible
			&& control.FocusMode != FocusModeEnum.None
			&& (control is not BaseButton button || !button.Disabled);
	}

	private void RestorePlayerRerollFocus(int slotIndex)
	{
		RestoreFocusDeferred(
			slotIndex >= 0 && slotIndex < _rerollButtons.Count && CanReceiveFocus(_rerollButtons[slotIndex])
				? _rerollButtons[slotIndex]
				: GetHolderForSlot(slotIndex));
	}

	private void RestoreEnemyRerollFocus(int slotIndex)
	{
		RestoreFocusDeferred(GetFocusableButton(_enemyHexRerollButtons, slotIndex) ?? GetHolderForSlot(slotIndex));
	}

	private void RestoreEnemyRemoveFocus(int slotIndex)
	{
		RestoreFocusDeferred(GetFocusableButton(_enemyHexRemoveButtons, slotIndex) ?? GetHolderForSlot(slotIndex));
	}

	private static Control? GetFocusableButton(IReadOnlyList<Button> buttons, int index)
	{
		return index >= 0 && index < buttons.Count && CanReceiveFocus(buttons[index])
			? buttons[index]
			: null;
	}

	private Control? GetHolderForSlot(int slotIndex)
	{
		return _holders.Count == 0
			? null
			: _holders[Math.Clamp(slotIndex, 0, _holders.Count - 1)];
	}

	private void RestoreFocusDeferred(Control? target)
	{
		if (!_controllerNavigationActivated || target == null)
		{
			return;
		}

		Callable.From(() =>
		{
			if (!_closed && CanReceiveFocus(target))
			{
				target.GrabFocus();
			}
		}).CallDeferred();
	}
}
