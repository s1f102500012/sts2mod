using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.addons.mega_text;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen
{
	private Control CreateEnemyPreview()
	{
		int rowCount = Math.Max(1, _monsterHexKinds.Count);
		float panelHeight = Math.Min(330f, Math.Max(104f, 28f + rowCount * 76f));
		PanelContainer panel = new()
		{
			Name = "EnemyPreviewPanel",
			CustomMinimumSize = new Vector2(1040f, panelHeight),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", CreatePreviewStyle());

		MarginContainer margin = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		ScrollContainer scroll = new()
		{
			Name = "EnemyPreviewScroll",
			MouseFilter = MouseFilterEnum.Pass,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		margin.AddChild(scroll);

		VBoxContainer rows = new()
		{
			Name = "EnemyPreviewRows",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		rows.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(rows);

		if (_monsterHexKinds.Count == 0)
		{
			rows.AddChild(CreateEnemyPreviewRow(-1));
		}
		else
		{
			for (int i = 0; i < _monsterHexKinds.Count; i++)
			{
				rows.AddChild(CreateEnemyPreviewRow(i));
			}
		}

		return panel;
	}

	private Control CreateEnemyPreviewRow(int slotIndex)
	{
		MonsterHexKind? monsterHex = GetMonsterHexSlot(_monsterHexKinds, slotIndex);
		RelicModel? monsterHexRelic = CreateMonsterHexRelic(monsterHex);
		HBoxContainer row = new()
		{
			Name = slotIndex >= 0 ? $"EnemyHexRow{slotIndex}" : "EnemyHexRowEmpty",
			CustomMinimumSize = new Vector2(0f, 68f),
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 14);

		CenterContainer iconBox = new()
		{
			CustomMinimumSize = new Vector2(56f, 56f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddChild(iconBox);
		if (monsterHexRelic != null)
		{
			TextureRect enemyTexture = CreateRelicTexture(monsterHexRelic, 54f);
			iconBox.AddChild(enemyTexture);
			AttachRelicHoverTips(enemyTexture, monsterHexRelic, monsterHex);
		}
		else
		{
			MegaLabel removedIcon = new()
			{
				Text = "-",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MaxFontSize = 38,
				MinFontSize = 30
			};
			HextechUiTheme.ApplyDefaultMegaLabelTheme(removedIcon);
			removedIcon.Modulate = new Color(0.86f, 0.88f, 0.92f, 0.68f);
			iconBox.AddChild(removedIcon);
		}

		VBoxContainer textColumn = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		textColumn.AddThemeConstantOverride("separation", 3);
		row.AddChild(textColumn);

		HBoxContainer titleRow = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		titleRow.AddThemeConstantOverride("separation", 10);
		textColumn.AddChild(titleRow);

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			MaxFontSize = 22,
			MinFontSize = 17
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(title);
		title.Modulate = new Color(0.97f, 0.96f, 0.9f, 0.96f);
		title.SetTextAutoSize(monsterHexRelic != null
			? monsterHexRelic.Title.GetFormattedText()
			: new LocString(LocTable, "HEXTECH_ENEMY_REMOVED_TITLE").GetRawText());
		titleRow.AddChild(title);

		if (monsterHexRelic != null)
		{
			titleRow.AddChild(CreateRarityPill());
		}

		MegaRichTextLabel body = CreateDescriptionLabel();
		body.CustomMinimumSize = new Vector2(0f, 34f);
		if (monsterHex.HasValue)
		{
			SetFixedDescriptionText(body, MonsterHexCatalog.GetEnemyHexDescriptionFormatted(monsterHex.Value), 14);
		}
		else
		{
			SetFixedDescriptionText(body, new LocString(LocTable, "HEXTECH_ENEMY_REMOVED_DESCRIPTION").GetRawText(), 14);
		}
		textColumn.AddChild(body);

		if (_enemyHexControlsEnabled && slotIndex >= 0)
		{
			bool showUndoButton = ShouldShowEnemyHexUndoButton(monsterHex);
			HBoxContainer actionRow = new()
			{
				Name = $"EnemyHexActionRow{slotIndex}",
				MouseFilter = MouseFilterEnum.Pass,
				CustomMinimumSize = showUndoButton
					? EnemyUndoButtonSize
					: new Vector2(EnemyRerollButtonSize.X + EnemyRemoveButtonSize.X + 10f, EnemyRerollButtonSize.Y),
				SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
				Alignment = BoxContainer.AlignmentMode.Center
			};
			actionRow.AddThemeConstantOverride("separation", 10);
			row.AddChild(actionRow);

			Button rerollButton;
			if (showUndoButton)
			{
				rerollButton = new Button
				{
					Name = $"EnemyHexRerollButton_{slotIndex}",
					Visible = false,
					Disabled = true,
					FocusMode = FocusModeEnum.None
				};
			}
			else
			{
				bool rerollDisabled = _enemyHexRerollFunc == null || IsEnemyHexRerollLimitReached(slotIndex);
				rerollButton = CreateRerollIconButton(
					$"EnemyHexRerollButton_{slotIndex}",
					EnemyRerollButtonSize,
					rerollDisabled,
					includeGoldenVisual: false);
				rerollButton.Pressed += () => OnEnemyHexRerollPressed(slotIndex);
			}
			actionRow.AddChild(rerollButton);
			_enemyHexRerollButtons.Add(rerollButton);

			bool canUndoRemove = !monsterHex.HasValue && GetMonsterHexBeforeRemovalSlot(slotIndex).HasValue;
			Button removeButton = CreateEnemyHexRemovalButton(
				slotIndex,
				showUndoButton,
				disabled: showUndoButton && !canUndoRemove);
			removeButton.Pressed += () => OnEnemyHexRemovePressed(slotIndex);
			actionRow.AddChild(removeButton);
			_enemyHexRemoveButtons.Add(removeButton);
		}

		return row;
	}

	private Button CreateEnemyHexRemovalButton(int slotIndex, bool undo, bool disabled)
	{
		Vector2 buttonSize = undo ? EnemyUndoButtonSize : EnemyRemoveButtonSize;
		Button button = new()
		{
			Name = undo ? $"EnemyHexUndoButton_{slotIndex}" : $"EnemyHexRemoveButton_{slotIndex}",
			Text = string.Empty,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = buttonSize,
			Disabled = disabled
		};
		StyleBoxEmpty emptyStyle = new();
		button.AddThemeStyleboxOverride("normal", emptyStyle);
		button.AddThemeStyleboxOverride("hover", emptyStyle);
		button.AddThemeStyleboxOverride("pressed", emptyStyle);
		button.AddThemeStyleboxOverride("focus", emptyStyle);
		button.AddThemeStyleboxOverride("disabled", emptyStyle);

		TextureRect icon = new()
		{
			Name = undo ? "UndoButtonTexture" : "RemoveButtonTexture",
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = buttonSize,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		button.AddChild(icon);

		bool hovered = false;
		bool focused = false;
		bool pressed = false;
		void UpdateVisualState()
		{
			string normalPath = undo ? UndoButtonTexturePath : RemoveButtonTexturePath;
			string path = ResolveEnemyHexRemovalButtonTexture(undo, disabled, pressed, hovered || focused);
			icon.Texture = HextechTextures.LoadUiTexture(path)
				?? HextechTextures.LoadUiTexture(normalPath);
		}

		button.MouseEntered += () =>
		{
			hovered = true;
			UpdateVisualState();
		};
		button.MouseExited += () =>
		{
			hovered = false;
			UpdateVisualState();
		};
		button.FocusEntered += () =>
		{
			focused = true;
			UpdateVisualState();
		};
		button.FocusExited += () =>
		{
			focused = false;
			UpdateVisualState();
		};
		button.ButtonDown += () =>
		{
			pressed = true;
			UpdateVisualState();
		};
		button.ButtonUp += () =>
		{
			pressed = false;
			UpdateVisualState();
		};
		UpdateVisualState();
		return button;
	}

	internal static string ResolveEnemyHexRemovalButtonTexture(bool undo, bool disabled, bool pressed, bool highlighted)
	{
		if (undo)
		{
			return disabled
				? UndoButtonDisabledTexturePath
				: pressed
					? UndoButtonPressedTexturePath
					: highlighted ? UndoButtonHoverTexturePath : UndoButtonTexturePath;
		}

		return disabled
			? RemoveButtonDisabledTexturePath
			: pressed
				? RemoveButtonPressedTexturePath
				: highlighted ? RemoveButtonHoverTexturePath : RemoveButtonTexturePath;
	}

	internal static bool ShouldShowEnemyHexUndoButton(MonsterHexKind? monsterHex)
	{
		return !monsterHex.HasValue;
	}

	private MonsterHexKind? GetMonsterHexBeforeRemovalSlot(int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < _monsterHexBeforeRemoval.Count
			? _monsterHexBeforeRemoval[slotIndex]
			: null;
	}

}
