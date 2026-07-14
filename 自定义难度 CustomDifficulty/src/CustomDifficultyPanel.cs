using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace CustomDifficulty;

internal static class CustomDifficultyPanel
{
	private const string PanelName = "CustomDifficultyPanel";
	private const float PreferredPanelWidth = 540f;
	private const float MinimumPanelWidth = 360f;
	private const float PanelMargin = 16f;

	private static PanelContainer? _root;
	private static Button? _fixedModeButton;
	private static Button? _progressiveModeButton;
	private static VBoxContainer? _fixedSection;
	private static VBoxContainer? _progressiveSection;
	private static HSlider? _hpSlider;
	private static HSlider? _attackSlider;
	private static Label? _hpValueLabel;
	private static Label? _attackValueLabel;
	private static HSlider? _hpDeltaSlider;
	private static HSlider? _attackDeltaSlider;
	private static Label? _hpDeltaValueLabel;
	private static Label? _attackDeltaValueLabel;
	private static Label? _statusLabel;
	private static bool _refreshing;

	public static void Inject(NCharacterSelectScreen screen)
	{
		try
		{
			RemoveFrom(screen);
			_root = BuildPanel();
			screen.AddChild(_root);
			screen.MoveChild(_root, screen.GetChildCount() - 1);
			PlacePanel(_root);
			Refresh();
			CustomDifficultySettings.Changed -= Refresh;
			CustomDifficultySettings.Changed += Refresh;
			Log.Info($"[{ModInfo.Id}] Character select panel injected.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to inject character select panel: {ex}");
		}
	}

	public static void RemoveFrom(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child.Name == PanelName)
			{
				node.RemoveChild(child);
				child.QueueFree();
			}
		}

		if (_root != null && !GodotObject.IsInstanceValid(_root))
		{
			ClearReferences();
		}
	}

	public static void Refresh()
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			ClearReferences();
			return;
		}

		_refreshing = true;
		try
		{
			bool canEdit = CustomDifficultySync.CanLocalEdit;
			bool progressive = CustomDifficultySettings.Mode == CustomDifficultyMode.Progressive;

			RefreshModeButton(_fixedModeButton, !progressive, canEdit);
			RefreshModeButton(_progressiveModeButton, progressive, canEdit);
			if (_fixedSection != null)
			{
				_fixedSection.Visible = !progressive;
			}
			if (_progressiveSection != null)
			{
				_progressiveSection.Visible = progressive;
			}

			RefreshSlider(_hpSlider, CustomDifficultySettings.MonsterHpSliderValue, canEdit);
			RefreshSlider(_attackSlider, CustomDifficultySettings.MonsterAttackSliderValue, canEdit);
			if (_hpValueLabel != null)
			{
				_hpValueLabel.Text = CustomDifficultySettings.FormatMultiplier(CustomDifficultySettings.MonsterHpTicks);
			}
			if (_attackValueLabel != null)
			{
				_attackValueLabel.Text = CustomDifficultySettings.FormatMultiplier(CustomDifficultySettings.MonsterAttackTicks);
			}

			RefreshSlider(_hpDeltaSlider, CustomDifficultySettings.HpDeltaPercentPerRoom, canEdit);
			RefreshSlider(_attackDeltaSlider, CustomDifficultySettings.AttackDeltaPercentPerRoom, canEdit);
			if (_hpDeltaValueLabel != null)
			{
				_hpDeltaValueLabel.Text = CustomDifficultySettings.FormatDeltaPercent(CustomDifficultySettings.HpDeltaPercentPerRoom);
			}
			if (_attackDeltaValueLabel != null)
			{
				_attackDeltaValueLabel.Text = CustomDifficultySettings.FormatDeltaPercent(CustomDifficultySettings.AttackDeltaPercentPerRoom);
			}

			if (_statusLabel != null)
			{
				_statusLabel.Text = GetStatusText();
				_statusLabel.AddThemeColorOverride("font_color", canEdit ? StsColors.green : StsColors.gray);
			}
		}
		finally
		{
			_refreshing = false;
		}
	}

	private static PanelContainer BuildPanel()
	{
		PanelContainer panel = new()
		{
			Name = PanelName,
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 360,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 0f,
			AnchorBottom = 0f,
			// 高度 0 = 按内容自适应，模式切换后自动收缩，无多余留白
			CustomMinimumSize = new Vector2(PreferredPanelWidth, 0f),
			Size = new Vector2(PreferredPanelWidth, 0f)
		};
		ApplyPanelStyle(panel);

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
		stack.AddThemeConstantOverride("separation", 8);
		margin.AddChild(stack);

		Label title = CreateLabel(ModInfo.Name, 19, StsColors.gold);
		stack.AddChild(title);

		// 顶部模式页签：全局模式 / 递进模式，二选一，选中后切换下方布局。
		HBoxContainer modeRow = new()
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		modeRow.AddThemeConstantOverride("separation", 8);
		ButtonGroup modeGroup = new();
		_fixedModeButton = CreateModeButton("全局模式", "全程固定的怪物血量/攻击倍率。", modeGroup);
		_progressiveModeButton = CreateModeButton("递进模式", "每前进一个房间按百分比叠加难度（配合无尽模式 0.4.0+ 可跨轮持续叠加）。", modeGroup);
		modeRow.AddChild(_fixedModeButton);
		modeRow.AddChild(_progressiveModeButton);
		stack.AddChild(modeRow);

		_fixedSection = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		_fixedSection.AddThemeConstantOverride("separation", 8);
		_hpSlider = CreateSlider(0.1, 5.0, 0.1, 1.0);
		_hpValueLabel = CreateValueLabel("x1.0");
		_fixedSection.AddChild(CreateSliderRow("怪物血量", _hpSlider, _hpValueLabel));
		_attackSlider = CreateSlider(0.1, 5.0, 0.1, 1.0);
		_attackValueLabel = CreateValueLabel("x1.0");
		_fixedSection.AddChild(CreateSliderRow("怪物攻击", _attackSlider, _attackValueLabel));
		stack.AddChild(_fixedSection);

		_progressiveSection = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Pass,
			Visible = false
		};
		_progressiveSection.AddThemeConstantOverride("separation", 8);
		_hpDeltaSlider = CreateSlider(
			CustomDifficultySettings.MinDeltaPercent,
			CustomDifficultySettings.MaxDeltaPercent,
			1,
			CustomDifficultySettings.DefaultDeltaPercent);
		_hpDeltaValueLabel = CreateValueLabel("+2%");
		_progressiveSection.AddChild(CreateSliderRow("血量/房间", _hpDeltaSlider, _hpDeltaValueLabel));
		_attackDeltaSlider = CreateSlider(
			CustomDifficultySettings.MinDeltaPercent,
			CustomDifficultySettings.MaxDeltaPercent,
			1,
			CustomDifficultySettings.DefaultDeltaPercent);
		_attackDeltaValueLabel = CreateValueLabel("+2%");
		_progressiveSection.AddChild(CreateSliderRow("攻击/房间", _attackDeltaSlider, _attackDeltaValueLabel));
		stack.AddChild(_progressiveSection);

		_statusLabel = CreateLabel("", 13, StsColors.green);
		stack.AddChild(_statusLabel);

		_hpSlider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(OnHpValueChanged));
		_attackSlider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(OnAttackValueChanged));
		_fixedModeButton.Connect(BaseButton.SignalName.Toggled, Callable.From<bool>(pressed =>
		{
			if (pressed)
			{
				ApplyLocalChange(mode: CustomDifficultyMode.Fixed);
			}
		}));
		_progressiveModeButton.Connect(BaseButton.SignalName.Toggled, Callable.From<bool>(pressed =>
		{
			if (pressed)
			{
				ApplyLocalChange(mode: CustomDifficultyMode.Progressive);
			}
		}));
		_hpDeltaSlider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(OnHpDeltaChanged));
		_attackDeltaSlider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(OnAttackDeltaChanged));
		return panel;
	}

	private static Button CreateModeButton(string text, string tooltip, ButtonGroup group)
	{
		Button button = new()
		{
			Text = text,
			ToggleMode = true,
			ButtonGroup = group,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.All,
			CustomMinimumSize = new Vector2(120f, 32f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			TooltipText = tooltip
		};
		ApplyGameThemeFont(button);
		button.AddThemeFontSizeOverride("font_size", 15);
		button.AddThemeColorOverride("font_color", new Color(0.8f, 0.79f, 0.72f));
		button.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.93f, 0.84f));
		button.AddThemeColorOverride("font_pressed_color", StsColors.gold);
		button.AddThemeColorOverride("font_hover_pressed_color", StsColors.gold);
		button.AddThemeColorOverride("font_disabled_color", new Color(0.6f, 0.6f, 0.6f, 0.8f));
		button.AddThemeStyleboxOverride("normal", CreateModeButtonStyle(selected: false, hover: false));
		button.AddThemeStyleboxOverride("hover", CreateModeButtonStyle(selected: false, hover: true));
		button.AddThemeStyleboxOverride("pressed", CreateModeButtonStyle(selected: true, hover: false));
		button.AddThemeStyleboxOverride("hover_pressed", CreateModeButtonStyle(selected: true, hover: true));
		button.AddThemeStyleboxOverride("disabled", CreateModeButtonStyle(selected: false, hover: false));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		return button;
	}

	private static StyleBoxFlat CreateModeButtonStyle(bool selected, bool hover)
	{
		StyleBoxFlat style = new()
		{
			BgColor = selected
				? new Color(0.28f, 0.22f, 0.08f, 0.92f)
				: new Color(0.08f, 0.09f, 0.11f, hover ? 0.95f : 0.85f),
			BorderColor = selected
				? new Color(0.95f, 0.78f, 0.22f, 0.95f)
				: new Color(0.55f, 0.52f, 0.44f, hover ? 0.8f : 0.55f)
		};
		style.SetBorderWidthAll(selected ? 2 : 1);
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 4;
		style.ContentMarginBottom = 4;
		return style;
	}

	private static void RefreshModeButton(Button? button, bool pressed, bool canEdit)
	{
		if (button == null)
		{
			return;
		}

		button.ButtonPressed = pressed;
		button.Disabled = !canEdit;
		button.FocusMode = canEdit ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
	}

	private static HBoxContainer CreateSliderRow(string labelText, HSlider slider, Label valueLabel)
	{
		HBoxContainer row = new()
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", 10);

		Label label = CreateLabel(labelText, 15, StsColors.cream);
		label.CustomMinimumSize = new Vector2(82f, 26f);
		row.AddChild(label);
		row.AddChild(slider);
		row.AddChild(valueLabel);
		return row;
	}

	private static HSlider CreateSlider(double min, double max, double step, double value)
	{
		return new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = value,
			CustomMinimumSize = new Vector2(320f, 26f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.All
		};
	}

	private static Label CreateValueLabel(string text)
	{
		Label label = CreateLabel(text, 15, StsColors.gold);
		label.HorizontalAlignment = HorizontalAlignment.Right;
		label.CustomMinimumSize = new Vector2(48f, 26f);
		return label;
	}

	// 用游戏自带的 MegaLabel（含按语言的字体替换）替代裸 Godot Label，
	// 后者走引擎默认字体，缩放后发虚；MegaLabel 与原版 UI 同一渲染路径。
	private static Label CreateLabel(string text, int fontSize, Color color)
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
			Log.Warn($"[{ModInfo.Id}] Failed to apply game theme font: {ex.Message}");
		}
	}

	private static void RefreshSlider(HSlider? slider, double value, bool canEdit)
	{
		if (slider == null)
		{
			return;
		}

		slider.Value = value;
		slider.Editable = canEdit;
		slider.FocusMode = canEdit ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
		slider.MouseFilter = canEdit ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		slider.Modulate = canEdit ? Colors.White : new Color(0.68f, 0.68f, 0.68f, 0.8f);
	}

	private static void OnHpValueChanged(double value)
	{
		ApplyLocalChange(hpTicks: CustomDifficultySettings.SliderValueToTicks(value));
	}

	private static void OnAttackValueChanged(double value)
	{
		ApplyLocalChange(attackTicks: CustomDifficultySettings.SliderValueToTicks(value));
	}

	private static void OnHpDeltaChanged(double value)
	{
		ApplyLocalChange(hpDelta: (int)Math.Round(value, MidpointRounding.AwayFromZero));
	}

	private static void OnAttackDeltaChanged(double value)
	{
		ApplyLocalChange(attackDelta: (int)Math.Round(value, MidpointRounding.AwayFromZero));
	}

	private static void ApplyLocalChange(
		int? hpTicks = null,
		int? attackTicks = null,
		CustomDifficultyMode? mode = null,
		int? hpDelta = null,
		int? attackDelta = null)
	{
		if (_refreshing || !CustomDifficultySync.CanLocalEdit)
		{
			return;
		}

		CustomDifficultySettings.SetLocal(
			hpTicks ?? CustomDifficultySettings.MonsterHpTicks,
			attackTicks ?? CustomDifficultySettings.MonsterAttackTicks,
			mode ?? CustomDifficultySettings.Mode,
			hpDelta ?? CustomDifficultySettings.HpDeltaPercentPerRoom,
			attackDelta ?? CustomDifficultySettings.AttackDeltaPercentPerRoom,
			broadcast: true);
	}

	private static string GetStatusText()
	{
		string modeText = CustomDifficultySettings.Mode == CustomDifficultyMode.Progressive
			? $"递进中：血量{CustomDifficultySettings.FormatDeltaPercent(CustomDifficultySettings.HpDeltaPercentPerRoom)}、攻击{CustomDifficultySettings.FormatDeltaPercent(CustomDifficultySettings.AttackDeltaPercentPerRoom)}每房间"
			: "全局固定倍率";
		string editText = CustomDifficultySync.CurrentGameType switch
		{
			NetGameType.Host => "房主可调整；会同步给加入玩家",
			NetGameType.Client => "仅房主可调整",
			_ => "单人模式可调整"
		};
		return $"{modeText}｜{editText}";
	}

	private static void PlacePanel(PanelContainer panel)
	{
		Vector2 viewportSize = panel.GetViewportRect().Size;
		float availableWidth = Math.Max(MinimumPanelWidth, viewportSize.X - PanelMargin * 2f);
		float panelWidth = Math.Min(PreferredPanelWidth, availableWidth);
		panel.CustomMinimumSize = new Vector2(panelWidth, 0f);
		panel.Size = new Vector2(panelWidth, 0f);
		panel.Position = new Vector2(
			Math.Max(PanelMargin, (viewportSize.X - panelWidth) / 2f),
			PanelMargin);
	}

	private static void ApplyPanelStyle(PanelContainer panel)
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.84f),
			BorderColor = new Color(0.86f, 0.68f, 0.28f, 0.7f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		panel.AddThemeStyleboxOverride("panel", style);
	}

	private static void ClearReferences()
	{
		_root = null;
		_fixedModeButton = null;
		_progressiveModeButton = null;
		_fixedSection = null;
		_progressiveSection = null;
		_hpSlider = null;
		_attackSlider = null;
		_hpValueLabel = null;
		_attackValueLabel = null;
		_hpDeltaSlider = null;
		_attackDeltaSlider = null;
		_hpDeltaValueLabel = null;
		_attackDeltaValueLabel = null;
		_statusLabel = null;
	}
}
