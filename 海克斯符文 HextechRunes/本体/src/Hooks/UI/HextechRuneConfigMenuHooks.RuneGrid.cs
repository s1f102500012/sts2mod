using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechRuneConfigMenuHooks
{
	private static Vector2 GetResponsivePanelSize()
	{
		Vector2I windowSize = DisplayServer.WindowGetSize();
		float windowWidth = windowSize.X > 0 ? windowSize.X : 1280f;
		float windowHeight = windowSize.Y > 0 ? windowSize.Y : 720f;
		bool compactLayout = windowHeight < CompactConfigHeightThreshold;
		// Panel must always be wide enough to hold the rune grid plus its own margins so the
		// border width stays constant across pages. Keep an upper bound for very wide screens.
		float panelMargins = (compactLayout ? 20f : 28f) * 2f;
		float minWidth = GetRuneGridMinWidth(compactLayout) + panelMargins;
		float maxWidth = Math.Max(minWidth, 1080f);
		float width = windowWidth < minWidth
			? Math.Max(320f, windowWidth * 0.98f)
			: Mathf.Clamp(windowWidth * 0.9f, minWidth, maxWidth);
		float height = windowHeight < CompactConfigHeightThreshold
			? Math.Max(440f, windowHeight * 0.98f)
			: Mathf.Clamp(windowHeight * 0.92f, 660f, 840f);
		return new Vector2(width, height);
	}

	private static float GetRuneGridMinWidth(bool compactLayout)
	{
		float rowSeparation = compactLayout ? 6f : 8f;
		float cardMargins = (compactLayout ? 14f : 20f) * 2f;
		return RuneConfigColumns * RuneConfigCellWidth
			+ (RuneConfigColumns - 1) * rowSeparation
			+ cardMargins;
	}

	private static bool IsCompactConfigLayout()
	{
		Vector2I windowSize = DisplayServer.WindowGetSize();
		float windowHeight = windowSize.Y > 0 ? windowSize.Y : 720f;
		return windowHeight < CompactConfigHeightThreshold;
	}

	private static Button CreateStepButton(string text, bool compactLayout)
	{
		Button button = new()
		{
			Text = string.Empty,
			FocusMode = Control.FocusModeEnum.All,
			CustomMinimumSize = compactLayout ? new Vector2(34f, 32f) : new Vector2(38f, 34f),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.1f, 0.12f, 0.17f, 0.9f), new Color(0.46f, 0.55f, 0.68f, 0.78f)));
		button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.13f, 0.16f, 0.22f, 0.95f), new Color(0.88f, 0.72f, 0.36f, 0.92f)));
		button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.07f, 0.09f, 0.13f, 0.98f), new Color(0.88f, 0.62f, 0.28f, 0.92f)));
		AddCrispButtonText(button, text, compactLayout ? 17 : 18, new Color(0.96f, 0.94f, 0.88f, 1f));
		return button;
	}

	private static void AttachRepeatingStep(Button button, Action action)
	{
		int pressToken = 0;

		button.ButtonDown += () =>
		{
			if (button.Disabled)
			{
				return;
			}

			pressToken++;
			int currentToken = pressToken;
			action();
			TaskHelper.RunSafely(RepeatStepAsync(button, currentToken, () => pressToken == currentToken, action));
		};
		button.ButtonUp += () => pressToken++;
		button.TreeExiting += () => pressToken++;
	}

	private static async Task RepeatStepAsync(Button button, int token, Func<bool> tokenIsCurrent, Action action)
	{
		if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
		{
			return;
		}

		SceneTree tree = button.GetTree();
		if (tree == null)
		{
			return;
		}

		await button.ToSignal(tree.CreateTimer(StepRepeatInitialDelaySeconds), "timeout");
		int repeatCount = 0;
		while (GodotObject.IsInstanceValid(button)
			&& button.IsInsideTree()
			&& button.ButtonPressed
			&& !button.Disabled
			&& tokenIsCurrent())
		{
			action();
			repeatCount++;
			float interval = repeatCount >= StepRepeatFastAfterTicks
				? StepRepeatFastIntervalSeconds
				: StepRepeatIntervalSeconds;
			await button.ToSignal(tree.CreateTimer(interval), "timeout");
		}
	}

	private static bool IsEnemyHexCountConfigReadOnly()
	{
		return HextechPlayerContextHelper.IsClientRun();
	}

	private static void CloseWithoutSaving(Control overlay)
	{
		if (!GodotObject.IsInstanceValid(overlay))
		{
			return;
		}

		overlay.GetViewport()?.SetInputAsHandled();
		CloseOverlayAnimated(overlay);
	}

	private static Label CreateSectionHeader(string text, int fontSize = 20)
	{
		Label label = CreateLabel(text, fontSize, new Color(0.96f, 0.84f, 0.48f, 0.98f));
		label.CustomMinimumSize = new Vector2(0f, fontSize + 6f);
		return label;
	}

	private static Label CreateSourceHeader(string text, bool compactLayout)
	{
		Label label = CreateLabel(text, compactLayout ? 14 : 15, new Color(0.68f, 0.82f, 0.98f, 0.92f));
		label.CustomMinimumSize = new Vector2(0f, compactLayout ? 18f : 22f);
		return label;
	}

	private static VBoxContainer CreateRuneGrid(bool compactLayout)
	{
		VBoxContainer grid = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		grid.AddThemeConstantOverride("separation", compactLayout ? 5 : 7);
		return grid;
	}

	private static HBoxContainer CreateRuneRow(bool compactLayout)
	{
		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 6 : 8);
		return row;
	}

	private static CenterContainer CreateRuneSlot()
	{
		return new CenterContainer()
		{
			CustomMinimumSize = new Vector2(RuneConfigCellWidth, RuneConfigCellHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
	}

	private static RuneIconBinding CreateRuneIcon(RuneConfigEntry entry, HashSet<string> pendingDisabledIds, Action updateSummary)
	{
		VBoxContainer root = new()
		{
			Name = "RuneConfigIcon_" + entry.Id,
			CustomMinimumSize = new Vector2(RuneConfigCellWidth, RuneConfigCellHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		root.AddThemeConstantOverride("separation", 2);

		Control iconLayer = new()
		{
			CustomMinimumSize = new Vector2(RuneConfigCellWidth, RuneConfigIconLayerHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		CenterContainer iconCenter = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		ApplyConfigIconScale(iconCenter);
		NRelicBasicHolder holder = NRelicBasicHolder.Create(entry.Relic)
			?? throw new InvalidOperationException($"Failed to create config relic holder for {entry.Id}.");
		holder.MouseFilter = Control.MouseFilterEnum.Ignore;
		iconCenter.AddChild(holder);
		iconLayer.AddChild(iconCenter);
		root.AddChild(iconLayer);

		Label title = CreateRuneNameLabel(entry.Title);
		root.AddChild(title);

		RuneIconBinding binding = new(entry.Id, root, holder, title);
		ApplyRuneIconState(binding, !pendingDisabledIds.Contains(entry.Id));
		AttachRuneToggleInput(root, entry, binding, pendingDisabledIds, updateSummary);
		AttachRelicHoverTips(root, entry.Relic, GetEnemyHexKind(entry));
		root.FocusEntered += () => root.SelfModulate = new Color(1.12f, 1.12f, 1.12f, 1f);
		root.FocusExited += () => root.SelfModulate = Colors.White;
		return binding;
	}

	private static void ApplyConfigIconScale(Control control)
	{
		control.Scale = Vector2.One * ConfigRuneHolderScale;
		control.PivotOffset = new Vector2(RuneConfigCellWidth, RuneConfigIconLayerHeight) * 0.5f;
		control.Resized += () =>
		{
			if (GodotObject.IsInstanceValid(control))
			{
				control.PivotOffset = control.Size * 0.5f;
			}
		};
	}

	private static async Task PopulateRuneIconsAsync(Control overlay, RuneConfigOverlayState state)
	{
		if (!await HextechGodotAsync.AwaitProcessFrameAsync(overlay))
		{
			return;
		}

		int loadedThisFrame = 0;
		foreach (RuneConfigLoadTarget target in state.LoadTargets)
		{
			if (!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree())
			{
				return;
			}

			RuneIconBinding binding = CreateRuneIcon(target.Entry, target.PendingDisabledIds, state.UpdateSummary);
			if (ReferenceEquals(target.PendingDisabledIds, state.PendingDisabledPlayerIds))
			{
				state.PlayerIconBindings.Add(binding);
			}
			else if (ReferenceEquals(target.PendingDisabledIds, state.PendingDisabledMonsterHexIds))
			{
				state.EnemyIconBindings.Add(binding);
			}
			else
			{
				state.ForgeIconBindings.Add(binding);
			}
			target.Grid.AddChild(binding.Root);
			WireControllerFocusScrolling(binding.Root);

			loadedThisFrame++;
			if (loadedThisFrame < RuneConfigIconsPerFrame)
			{
				continue;
			}

			loadedThisFrame = 0;
			if (!await HextechGodotAsync.AwaitProcessFrameAsync(overlay))
			{
				return;
			}
		}
	}

	private static Label CreateRuneNameLabel(string text)
	{
		Label label = CreateLabel(text, 13, new Color(0.96f, 0.97f, 1f, 1f));
		label.CustomMinimumSize = new Vector2(108f, 38f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Top;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.ClipText = true;
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
		label.AddThemeConstantOverride("outline_size", 2);
		return label;
	}

	private static void AttachRuneToggleInput(
		Control root,
		RuneConfigEntry entry,
		RuneIconBinding binding,
		HashSet<string> pendingDisabledIds,
		Action updateSummary)
	{
		bool pointerPressed = false;
		bool pointerDragged = false;
		bool longPressShown = false;
		int pointerToken = 0;
		Vector2 pressPosition = Vector2.Zero;

		root.GuiInput += inputEvent =>
		{
			if (inputEvent.IsActionPressed("ui_accept"))
			{
				root.GetViewport()?.SetInputAsHandled();
				ToggleRune(entry.Id, binding, pendingDisabledIds, updateSummary);
				return;
			}

			switch (inputEvent)
			{
				case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton:
					if (mouseButton.Pressed)
					{
						BeginRunePress(mouseButton.Position, false);
					}
					else
					{
						EndRunePress();
					}
					break;
				case InputEventMouseMotion mouseMotion when pointerPressed:
					UpdateRuneDrag(mouseMotion.Position);
					break;
				case InputEventScreenTouch screenTouch:
					if (screenTouch.Pressed)
					{
						BeginRunePress(screenTouch.Position, true);
					}
					else
					{
						EndRunePress();
					}
					break;
				case InputEventScreenDrag screenDrag when pointerPressed:
					UpdateRuneDrag(screenDrag.Position);
					break;
			}
		};

		void BeginRunePress(Vector2 position, bool touch)
		{
			pointerPressed = true;
			pointerDragged = false;
			longPressShown = false;
			pressPosition = position;
			pointerToken++;
			if (touch)
			{
				int currentToken = pointerToken;
				TaskHelper.RunSafely(ShowTouchHoverTipAfterDelay(root, entry.Relic, GetEnemyHexKind(entry), currentToken, () => pointerToken == currentToken && pointerPressed && !pointerDragged, () => longPressShown = true));
			}
		}

		void UpdateRuneDrag(Vector2 position)
		{
			if (pressPosition.DistanceTo(position) <= RuneConfigDragThreshold)
			{
				return;
			}

			pointerDragged = true;
			NHoverTipSet.Remove(root);
		}

		void EndRunePress()
		{
			if (!pointerPressed)
			{
				return;
			}

			pointerPressed = false;
			pointerToken++;
			if (!pointerDragged && !longPressShown)
			{
				root.GetViewport()?.SetInputAsHandled();
				ToggleRune(entry.Id, binding, pendingDisabledIds, updateSummary);
			}
			else if (longPressShown)
			{
				root.GetViewport()?.SetInputAsHandled();
			}

			NHoverTipSet.Remove(root);
		}
	}

	private static async Task ShowTouchHoverTipAfterDelay(
		Control holder,
		RelicModel relic,
		MonsterHexKind? monsterHex,
		int token,
		Func<bool> shouldShow,
		Action onShown)
	{
		if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree())
		{
			return;
		}

		SceneTree tree = holder.GetTree();
		if (tree == null)
		{
			return;
		}

		await holder.ToSignal(tree.CreateTimer(RuneConfigLongPressSeconds), "timeout");
		if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !shouldShow())
		{
			return;
		}

		ShowRelicHoverTips(holder, relic, monsterHex);
		onShown();
		holder.GetViewport()?.SetInputAsHandled();
	}

	private static Button CreateActionButton(string text, Action action, bool compactLayout = false)
	{
		Button button = new()
		{
			Text = string.Empty,
			FocusMode = Control.FocusModeEnum.All,
			CustomMinimumSize = compactLayout ? new Vector2(112f, 34f) : new Vector2(132f, 38f),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.1f, 0.12f, 0.17f, 0.9f), new Color(0.46f, 0.55f, 0.68f, 0.78f)));
		button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.13f, 0.16f, 0.22f, 0.95f), new Color(0.88f, 0.72f, 0.36f, 0.92f)));
		button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.07f, 0.09f, 0.13f, 0.98f), new Color(0.88f, 0.62f, 0.28f, 0.92f)));
		button.AddThemeStyleboxOverride("focus", CreateButtonStyle(new Color(0.13f, 0.16f, 0.22f, 0.95f), new Color(0.88f, 0.72f, 0.36f, 0.92f)));
		AddCrispButtonText(button, text, compactLayout ? 14 : 16, new Color(0.96f, 0.94f, 0.88f, 1f));
		button.Pressed += action;
		return button;
	}

	private static void UpdateAllRuneIcons(IReadOnlyList<RuneIconBinding> bindings, IReadOnlySet<string> pendingDisabledIds)
	{
		foreach (RuneIconBinding binding in bindings)
		{
			ApplyRuneIconState(binding, !pendingDisabledIds.Contains(binding.Id));
		}
	}

	private static void ApplyRuneIconState(RuneIconBinding binding, bool enabled, bool animated = false)
	{
		Color holderTarget = enabled
			? Colors.White
			: new Color(0.34f, 0.36f, 0.4f, 0.44f);
		Color titleTarget = enabled
			? Colors.White
			: new Color(0.6f, 0.64f, 0.72f, 0.58f);

		if (!animated || !GodotObject.IsInstanceValid(binding.Root) || !binding.Root.IsInsideTree())
		{
			binding.Holder.Modulate = holderTarget;
			binding.Title.Modulate = titleTarget;
			return;
		}

		Tween tween = binding.Root.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(binding.Holder, "modulate", holderTarget, RuneStateFadeSeconds).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(binding.Title, "modulate", titleTarget, RuneStateFadeSeconds).SetEase(Tween.EaseType.Out);
	}

	private static void ToggleRune(string id, RuneIconBinding binding, HashSet<string> pendingDisabledIds, Action updateSummary)
	{
		if (pendingDisabledIds.Contains(id))
		{
			pendingDisabledIds.Remove(id);
		}
		else
		{
			pendingDisabledIds.Add(id);
		}

		ApplyRuneIconState(binding, !pendingDisabledIds.Contains(id), animated: true);
		PlayRuneToggleFeedback(binding.Root);
		updateSummary();
	}

	private static void PlayRuneToggleFeedback(Control root)
	{
		if (!GodotObject.IsInstanceValid(root))
		{
			return;
		}

		root.PivotOffset = root.Size * 0.5f;
		Tween tween = root.CreateTween();
		tween.TweenProperty(root, "scale", Vector2.One * 1.06f, 0.055f);
		tween.TweenProperty(root, "scale", Vector2.One, 0.085f);
	}

	private static void AttachRelicHoverTips(Control holder, RelicModel relic, MonsterHexKind? monsterHex = null)
	{
		holder.MouseEntered += () => ShowRelicHoverTips(holder, relic, monsterHex);
		holder.MouseExited += () => NHoverTipSet.Remove(holder);
		holder.FocusEntered += () => ShowRelicHoverTips(holder, relic, monsterHex);
		holder.FocusExited += () => NHoverTipSet.Remove(holder);
		holder.TreeExiting += () => NHoverTipSet.Remove(holder);
	}

	private static void ConfigureHorizontalFocus(IReadOnlyList<Button> buttons)
	{
		for (int i = 0; i < buttons.Count; i++)
		{
			buttons[i].FocusNeighborLeft = buttons[Math.Max(0, i - 1)].GetPath();
			buttons[i].FocusNeighborRight = buttons[Math.Min(buttons.Count - 1, i + 1)].GetPath();
		}
	}

	private static void WireControllerFocusScrolling(Node node)
	{
		if (node is Control control && control.FocusMode != Control.FocusModeEnum.None && !control.HasMeta("hextech_focus_scroll"))
		{
			control.SetMeta("hextech_focus_scroll", true);
			control.FocusEntered += () =>
			{
				FindAncestor<ScrollContainer>(control)?.EnsureControlVisible(control);
			};
		}

		foreach (Node child in node.GetChildren())
		{
			WireControllerFocusScrolling(child);
		}
	}

	private static void ShowRelicHoverTips(Control holder, RelicModel relic, MonsterHexKind? monsterHex = null)
	{
		NHoverTipSet.Remove(holder);
		IEnumerable<IHoverTip> hoverTips = monsterHex.HasValue
			? MonsterHexCatalog.GetEnemyHexHoverTips(monsterHex.Value)
			: relic.HoverTips;
		NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(holder, hoverTips, HoverTip.GetHoverTipAlignment(holder));
		if (hoverTipSet == null)
		{
			return;
		}

		hoverTipSet.ZIndex = HoverTipZIndex;
		hoverTipSet.ZAsRelative = false;
		hoverTipSet.SetAlignment(holder, HoverTip.GetHoverTipAlignment(holder));
	}

	private static MonsterHexKind? GetEnemyHexKind(RuneConfigEntry entry)
	{
		return entry.PoolKey == "ENEMY" && Enum.TryParse(entry.Id, out MonsterHexKind monsterHex)
			? monsterHex
			: null;
	}
}
