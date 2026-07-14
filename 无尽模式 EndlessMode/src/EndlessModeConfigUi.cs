using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace EndlessMode;

internal static class EndlessModeConfigUi
{
	private const string PanelName = "EndlessModeConfigPanel";
	private static PanelContainer? _draggingPanel;
	private static Vector2 _dragOffset;

	public static void CharacterSelectReadyPostfix(NCharacterSelectScreen __instance)
	{
		try
		{
			InstallPanel(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModEntryConstants.ModId}] Character select config UI failed: {ex}");
		}
	}

	private static void InstallPanel(NCharacterSelectScreen characterSelect)
	{
		RemoveExistingPanel(characterSelect);
		bool canEdit = CanEdit(characterSelect);

		PanelContainer panel = new()
		{
			Name = PanelName,
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 350,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 0f,
			AnchorBottom = 0f,
			// 高度 0 = 交给 PanelContainer 按内容自适应，避免底部留白
			CustomMinimumSize = new Vector2(500f, 0f),
			Size = new Vector2(500f, 0f)
		};
		ApplyPanelStyle(panel);
		ConnectDragHandle(panel, panel);

		MarginContainer margin = new()
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		VBoxContainer stack = new()
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		stack.AddThemeConstantOverride("separation", 5);
		margin.AddChild(stack);

		Label title = CreateLabel("ENDLESS_MODE.config.title", "无尽模式配置", 19, new Color(0.95f, 0.78f, 0.22f));
		title.MouseFilter = Control.MouseFilterEnum.Stop;
		ConnectDragHandle(title, panel);
		stack.AddChild(title);
		stack.AddChild(CreateLabel(
			"ENDLESS_MODE.config.note",
			"只影响之后进入的新无尽轮次。联机时以房主配置为准。荒疫之矛和荒疫之盾始终获得。",
			13,
			new Color(0.92f, 0.88f, 0.76f, 0.9f)));

		stack.AddChild(CreatePlagueSliderRow(
			"ENDLESS_MODE.config.plague_spear",
			"荒疫之矛强化",
			EndlessModeConfig.CurrentPlagueSpearPercent,
			EndlessModeConfig.SetPlagueSpearPercent,
			canEdit));
		stack.AddChild(CreatePlagueSliderRow(
			"ENDLESS_MODE.config.plague_shield",
			"荒疫之盾强化",
			EndlessModeConfig.CurrentPlagueShieldPercent,
			EndlessModeConfig.SetPlagueShieldPercent,
			canEdit));

		stack.AddChild(CreateRewardCheckBox(
			EndlessOptionalReward.MimicInfestation,
			"ENDLESS_MODE.config.mimic",
			"获得遍地宝箱怪",
			canEdit));
		stack.AddChild(CreateRewardCheckBox(
			EndlessOptionalReward.TimeMaze,
			"ENDLESS_MODE.config.time_maze",
			"获得时间迷宫",
			canEdit));
		stack.AddChild(CreateRewardCheckBox(
			EndlessOptionalReward.Muzzle,
			"ENDLESS_MODE.config.muzzle",
			"获得嘴套",
			canEdit));
		stack.AddChild(CreateRewardCheckBox(
			EndlessOptionalReward.HorribleTrophy,
			"ENDLESS_MODE.config.horrible_trophy",
			"获得可怖奖杯",
			canEdit));

		characterSelect.AddChild(panel);
		characterSelect.MoveChild(panel, characterSelect.GetChildCount() - 1);
		PlacePanel(panel);
	}

	private static void PlacePanel(PanelContainer panel)
	{
		Vector2 viewportSize = panel.GetViewportRect().Size;
		Vector2 panelSize = panel.Size;
		panel.Position = new Vector2(
			Math.Max(24f, viewportSize.X - panelSize.X - 24f),
			128f);
	}

	private static void ConnectDragHandle(Control handle, PanelContainer panel)
	{
		handle.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(input => OnDragHandleGuiInput(panel, input)));
	}

	private static void OnDragHandleGuiInput(PanelContainer panel, InputEvent input)
	{
		if (input is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				_draggingPanel = panel;
				_dragOffset = panel.GetGlobalMousePosition() - panel.GlobalPosition;
				panel.GetViewport().SetInputAsHandled();
			}
			else if (ReferenceEquals(_draggingPanel, panel))
			{
				_draggingPanel = null;
				panel.GetViewport().SetInputAsHandled();
			}
		}
		else if (input is InputEventMouseMotion && ReferenceEquals(_draggingPanel, panel))
		{
			MovePanel(panel, panel.GetGlobalMousePosition() - _dragOffset);
			panel.GetViewport().SetInputAsHandled();
		}
	}

	private static void MovePanel(PanelContainer panel, Vector2 globalPosition)
	{
		Vector2 viewportSize = panel.GetViewportRect().Size;
		Vector2 panelSize = panel.Size;
		float maxX = Math.Max(0f, viewportSize.X - panelSize.X);
		float maxY = Math.Max(0f, viewportSize.Y - panelSize.Y);
		panel.GlobalPosition = new Vector2(
			Math.Clamp(globalPosition.X, 0f, maxX),
			Math.Clamp(globalPosition.Y, 0f, maxY));
	}

	private static HBoxContainer CreatePlagueSliderRow(string labelKey, string fallback, int percent, Action<int> setPercent, bool canEdit)
	{
		HBoxContainer row = new()
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", 10);

		Label label = CreateLabel(labelKey, fallback, 15, new Color(0.95f, 0.93f, 0.84f));
		label.CustomMinimumSize = new Vector2(118f, 28f);
		row.AddChild(label);

		HSlider slider = new()
		{
			MinValue = EndlessModeConfig.MinPlagueScalingPercent,
			MaxValue = EndlessModeConfig.MaxPlagueScalingPercent,
			Step = EndlessModeConfig.PlagueScalingPercentStep,
			Value = percent,
			Editable = canEdit,
			CustomMinimumSize = new Vector2(260f, 28f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = canEdit ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore,
			FocusMode = canEdit ? Control.FocusModeEnum.All : Control.FocusModeEnum.None,
			TooltipText = Text("ENDLESS_MODE.config.plague_tooltip", "进入下一轮无尽时写入荒疫遗物。0% 表示只叠层不强化。")
		};
		slider.Modulate = canEdit ? Colors.White : new Color(0.68f, 0.68f, 0.68f, 0.8f);
		row.AddChild(slider);

		Label valueLabel = CreateLiteralLabel(FormatPercent(percent), 15, new Color(0.95f, 0.78f, 0.22f));
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.CustomMinimumSize = new Vector2(50f, 28f);
		row.AddChild(valueLabel);

		slider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(value =>
		{
			int nextPercent = SliderValueToPercent(value);
			valueLabel.Text = FormatPercent(nextPercent);
			if (canEdit)
			{
				setPercent(nextPercent);
			}
		}));
		return row;
	}

	private static CheckBox CreateRewardCheckBox(EndlessOptionalReward reward, string labelKey, string fallback, bool canEdit)
	{
		CheckBox checkBox = new()
		{
			Text = Text(labelKey, fallback),
			ButtonPressed = EndlessModeConfig.IsRewardEnabled(reward),
			Disabled = !canEdit,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = canEdit ? Control.FocusModeEnum.All : Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(360f, 28f),
			TooltipText = Text("ENDLESS_MODE.config.tooltip", "关闭后，之后进入对应轮次的无尽模式时不会获得该遗物。")
		};
		ApplyGameThemeFont(checkBox);
		ApplyCheckBoxVisuals(checkBox);
		checkBox.AddThemeFontSizeOverride("font_size", 15);
		checkBox.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.84f));
		checkBox.Connect(BaseButton.SignalName.Toggled, Callable.From<bool>(enabled =>
		{
			if (canEdit)
			{
				EndlessModeConfig.SetRewardEnabled(reward, enabled);
			}
		}));
		return checkBox;
	}

	private static int SliderValueToPercent(double value)
	{
		return EndlessModeConfig.ClampPlagueScalingPercent((int)Math.Round(value, MidpointRounding.AwayFromZero));
	}

	private static string FormatPercent(int percent)
	{
		return EndlessModeConfig.ClampPlagueScalingPercent(percent).ToString("0") + "%";
	}

	private static Label CreateLabel(string key, string fallback, int fontSize, Color color)
	{
		return CreateLiteralLabel(Text(key, fallback), fontSize, color);
	}

	// 用游戏自带的 MegaLabel（含按语言的字体替换）替代裸 Godot Label，
	// 后者走引擎默认字体，缩放后发虚；MegaLabel 与原版 UI 同一渲染路径。
	private static Label CreateLiteralLabel(string text, int fontSize, Color color)
	{
		MegaLabel label = new()
		{
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutoSizeEnabled = false
		};
		ApplyGameThemeFont(label);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.72f));
		label.AddThemeConstantOverride("outline_size", 2);
		return label;
	}

	// Godot 默认勾选框贴图在深色底上非勾选态几乎不可见：改用程序生成的
	// 描边方框贴图（勾选=内部填充金色）；同时清空按钮 stylebox 内边距，
	// 让勾选框与上方文字左缘对齐。
	private static readonly Dictionary<(bool Checked, bool Disabled), ImageTexture> CheckBoxIconCache = new();

	private static void ApplyCheckBoxVisuals(CheckBox checkBox)
	{
		try
		{
			checkBox.AddThemeIconOverride("checked", GetCheckBoxIcon(isChecked: true, isDisabled: false));
			checkBox.AddThemeIconOverride("unchecked", GetCheckBoxIcon(isChecked: false, isDisabled: false));
			checkBox.AddThemeIconOverride("checked_disabled", GetCheckBoxIcon(isChecked: true, isDisabled: true));
			checkBox.AddThemeIconOverride("unchecked_disabled", GetCheckBoxIcon(isChecked: false, isDisabled: true));
			foreach (string styleName in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
			{
				checkBox.AddThemeStyleboxOverride(styleName, new StyleBoxEmpty());
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModEntryConstants.ModId}] Failed to apply checkbox visuals: {ex.Message}");
		}
	}

	private static ImageTexture GetCheckBoxIcon(bool isChecked, bool isDisabled)
	{
		(bool, bool) key = (isChecked, isDisabled);
		if (CheckBoxIconCache.TryGetValue(key, out ImageTexture? cached))
		{
			return cached;
		}

		const int size = 22;
		const int border = 2;
		const int markInset = 5;
		float alpha = isDisabled ? 0.45f : 1f;
		Color borderColor = new(0.88f, 0.86f, 0.78f, 0.95f * alpha);
		Color fillColor = new(0f, 0f, 0f, 0.4f * alpha);
		Color markColor = new(0.95f, 0.78f, 0.22f, alpha);

		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				bool isBorder = x < border || y < border || x >= size - border || y >= size - border;
				image.SetPixel(x, y, isBorder ? borderColor : fillColor);
			}
		}

		if (isChecked)
		{
			for (int y = markInset; y < size - markInset; y++)
			{
				for (int x = markInset; x < size - markInset; x++)
				{
					image.SetPixel(x, y, markColor);
				}
			}
		}

		ImageTexture texture = ImageTexture.CreateFromImage(image);
		CheckBoxIconCache[key] = texture;
		return texture;
	}

	private static void ApplyGameThemeFont(Control control)
	{
		try
		{
			Font? font = control.GetThemeDefaultFont();
			if (font != null)
			{
				control.AddThemeFontOverride("font", font);
			}

			FontControlUtils.ApplyLocaleFontSubstitution(control, FontType.Regular, "font");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModEntryConstants.ModId}] Failed to apply game theme font: {ex.Message}");
		}
	}

	private static void ApplyPanelStyle(PanelContainer panel)
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.04f, 0.055f, 0.075f, 0.76f),
			BorderColor = new Color(0.86f, 0.68f, 0.28f, 0.7f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		panel.AddThemeStyleboxOverride("panel", style);
	}

	private static string Text(string key, string fallback)
	{
		try
		{
			return LocString.Exists("events", key)
				? new LocString("events", key).GetFormattedText()
				: fallback;
		}
		catch
		{
			return fallback;
		}
	}

	private static bool CanEdit(NCharacterSelectScreen characterSelect)
	{
		try
		{
			return characterSelect.Lobby?.NetService?.Type != NetGameType.Client;
		}
		catch
		{
			return true;
		}
	}

	private static void RemoveExistingPanel(Node mainMenu)
	{
		foreach (Node child in mainMenu.GetChildren())
		{
			if (child.Name == PanelName)
			{
				mainMenu.RemoveChild(child);
				child.QueueFree();
			}
		}
	}
}
