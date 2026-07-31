using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

internal static partial class HextechRelicVisibilityHooks
{
	private const string LocTable = "relic_collection";

	private static Control CreateToggleRoot()
	{
		Control root = new()
		{
			Name = ToggleRootNodeName,
			CustomMinimumSize = ToggleRootSize,
			Size = ToggleRootSize,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		root.AnchorLeft = 0f;
		root.AnchorRight = 0f;
		root.AnchorTop = 0f;
		root.AnchorBottom = 0f;
		root.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		root.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

		VBoxContainer column = new()
		{
			Name = ToggleColumnNodeName,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		column.AddThemeConstantOverride("separation", 0);
		root.AddChild(column);

		Control box = CreateToggleBox();
		column.AddChild(box);

		Label label = new()
		{
			Name = ToggleLabelNodeName,
			Text = new LocString(LocTable, "HEXTECH_HIDE_UI_LABEL").GetRawText(),
			CustomMinimumSize = new Vector2(ToggleRootSize.X, 16f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", new Color(0.94f, 0.88f, 0.68f, 1f));
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.75f));
		label.AddThemeConstantOverride("outline_size", 2);
		column.AddChild(label);

		return root;
	}

	private static Control CreateToggleBox()
	{
		Control box = new()
		{
			Name = ToggleBoxNodeName,
			CustomMinimumSize = ToggleBoxSize,
			MouseFilter = Control.MouseFilterEnum.Pass
		};

		Control visuals = CreateTickboxVisuals();
		box.AddChild(visuals);

		Button button = new()
		{
			Name = ToggleButtonNodeName,
			ToggleMode = true,
			Flat = true,
			Text = string.Empty,
			TooltipText = new LocString(LocTable, "HEXTECH_HIDE_UI_TOOLTIP").GetRawText(),
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.All
		};
		button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		ApplyTransparentButtonStyle(button);
		button.Connect(BaseButton.SignalName.Toggled, Callable.From<bool>(OnToggleChanged));
		box.AddChild(button);

		return box;
	}

	private static Control CreateTickboxVisuals()
	{
		PackedScene scene = LoadTickboxVisualScene();
		Control visuals = scene.Instantiate<Control>();
		visuals.Name = ToggleVisualsNodeName;
		visuals.MouseFilter = Control.MouseFilterEnum.Ignore;
		visuals.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return visuals;
	}

	private static PackedScene LoadTickboxVisualScene()
	{
		try
		{
			if (PreloadManager.Cache.ContainsKey(TickboxVisualScenePath))
			{
				return PreloadManager.Cache.GetScene(TickboxVisualScenePath);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Failed to read cached tickbox scene; using ResourceLoader: {ex.Message}", 2);
		}

		return ResourceLoader.Load<PackedScene>(TickboxVisualScenePath, cacheMode: ResourceLoader.CacheMode.Reuse)
			?? throw new InvalidOperationException($"Could not load tickbox scene: {TickboxVisualScenePath}");
	}

	private static void ApplyTransparentButtonStyle(Button button)
	{
		StyleBoxEmpty empty = new();
		button.AddThemeStyleboxOverride("normal", empty);
		button.AddThemeStyleboxOverride("hover", empty);
		button.AddThemeStyleboxOverride("pressed", empty);
		button.AddThemeStyleboxOverride("focus", empty);
		button.AddThemeStyleboxOverride("disabled", empty);
		button.AddThemeStyleboxOverride("hover_pressed", empty);
	}

	private static Control? FindToggleRoot(NGlobalUi globalUi)
	{
		Control? direct = globalUi.GetNodeOrNull<Control>(ToggleRootNodeName);
		if (direct != null && GodotObject.IsInstanceValid(direct))
		{
			return direct;
		}

		return globalUi.FindChild(ToggleRootNodeName, recursive: true, owned: false) as Control;
	}

	private static void UpdateToggleVisualState(Control root, bool hideRelics)
	{
		string tickedPath = $"{ToggleColumnNodeName}/{ToggleBoxNodeName}/{ToggleVisualsNodeName}/Ticked";
		string notTickedPath = $"{ToggleColumnNodeName}/{ToggleBoxNodeName}/{ToggleVisualsNodeName}/NotTicked";
		if (root.GetNodeOrNull<Control>(tickedPath) is { } ticked)
		{
			ticked.Visible = hideRelics;
		}

		if (root.GetNodeOrNull<Control>(notTickedPath) is { } notTicked)
		{
			notTicked.Visible = !hideRelics;
		}
	}

	private static void EnsurePositionTimer(NGlobalUi globalUi, Control root)
	{
		if (root.GetNodeOrNull<Godot.Timer>(PositionTimerNodeName) != null)
		{
			return;
		}

		Godot.Timer timer = new()
		{
			Name = PositionTimerNodeName,
			WaitTime = 0.25,
			OneShot = false,
			Autostart = true
		};
		timer.Timeout += () =>
		{
			if (!GodotObject.IsInstanceValid(globalUi) || !GodotObject.IsInstanceValid(root))
			{
				return;
			}

			root.Visible = true;
			PositionToggleRoot(root);
		};
		root.AddChild(timer);
	}

	private static void PositionToggleRoot(Control root)
	{
		if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree())
		{
			return;
		}

		Vector2 viewportSize = root.GetViewportRect().Size;
		Vector2 position = GetFallbackPosition(viewportSize);
		if (_drawPileAnchor is { } anchor && GodotObject.IsInstanceValid(anchor) && anchor.IsInsideTree())
		{
			Rect2 anchorRect = anchor.GetGlobalRect();
			if (anchorRect.Size.X > 0f && anchorRect.Size.Y > 0f)
			{
				position = new Vector2(
					anchorRect.End.X + DrawPileGap,
					anchorRect.Position.Y + (anchorRect.Size.Y - ToggleRootSize.Y) / 2f);
			}
		}

		float maxX = MathF.Max(0f, viewportSize.X - ToggleRootSize.X);
		float maxY = MathF.Max(0f, viewportSize.Y - ToggleRootSize.Y);
		root.GlobalPosition = new Vector2(Math.Clamp(position.X, 0f, maxX), Math.Clamp(position.Y, 0f, maxY));
	}

	private static Vector2 GetFallbackPosition(Vector2 viewportSize)
	{
		return new Vector2(
			LeftFallbackPadding,
			MathF.Max(0f, viewportSize.Y - ToggleRootSize.Y - BottomFallbackPadding));
	}

	private static void RefreshToggleRootPosition()
	{
		NGlobalUi? globalUi = NRun.Instance?.GlobalUi;
		if (globalUi?.GetNodeOrNull<Control>(ToggleRootNodeName) is { } root && GodotObject.IsInstanceValid(root))
		{
			root.Visible = true;
			PositionToggleRoot(root);
		}
	}
}
