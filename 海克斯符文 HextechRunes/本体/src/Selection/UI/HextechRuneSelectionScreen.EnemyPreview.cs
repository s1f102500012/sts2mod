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
			HBoxContainer actionRow = new()
			{
				Name = $"EnemyHexActionRow{slotIndex}",
				MouseFilter = MouseFilterEnum.Pass,
				CustomMinimumSize = new Vector2(236f, 0f),
				SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
				Alignment = BoxContainer.AlignmentMode.Center
			};
			actionRow.AddThemeConstantOverride("separation", 10);
			row.AddChild(actionRow);

			Button rerollButton = CreateEnemyHexActionButton(new LocString(LocTable, "HEXTECH_REROLL").GetRawText(), 104f);
			rerollButton.Disabled = !monsterHex.HasValue || _enemyHexRerollFunc == null || IsEnemyHexRerollLimitReached(slotIndex);
			rerollButton.Pressed += () => OnEnemyHexRerollPressed(slotIndex);
			actionRow.AddChild(rerollButton);

			bool canUndoRemove = !monsterHex.HasValue && GetMonsterHexBeforeRemovalSlot(slotIndex).HasValue;
			Button removeButton = CreateEnemyHexActionButton(new LocString(LocTable, monsterHex.HasValue ? "HEXTECH_ENEMY_REMOVE" : "HEXTECH_ENEMY_UNDO_REMOVE").GetRawText(), 104f);
			removeButton.Disabled = !monsterHex.HasValue && !canUndoRemove;
			removeButton.Pressed += () => OnEnemyHexRemovePressed(slotIndex);
			actionRow.AddChild(removeButton);
		}

		return row;
	}

	private Button CreateEnemyHexActionButton(string text, float width)
	{
		Color accent = GetAccentColor();
		Button button = new()
		{
			Text = string.Empty,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(width, 40f)
		};
		button.AddThemeStyleboxOverride("normal", CreateRerollStyle(new Color(0.08f, 0.1f, 0.15f, 0.72f), accent.Lightened(0.05f)));
		button.AddThemeStyleboxOverride("hover", CreateRerollStyle(new Color(0.1f, 0.13f, 0.18f, 0.82f), accent));
		button.AddThemeStyleboxOverride("pressed", CreateRerollStyle(new Color(0.07f, 0.09f, 0.13f, 0.86f), accent.Lightened(0.12f)));
		button.AddThemeStyleboxOverride("focus", CreateRerollStyle(new Color(0.1f, 0.13f, 0.18f, 0.82f), accent));
		button.AddThemeStyleboxOverride("disabled", CreateRerollStyle(new Color(0.08f, 0.09f, 0.12f, 0.56f), accent.Darkened(0.35f)));
		AddCrispButtonText(button, text, 16, new Color(0.94f, 0.92f, 0.86f, 1f));
		return button;
	}

	private void AddCrispButtonText(Button button, string text, int fontSize, Color fontColor)
	{
		MegaLabel label = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MinFontSize = fontSize,
			MaxFontSize = fontSize
		};
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		HextechUiTheme.ApplyDefaultMegaLabelTheme(label);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", fontColor);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.62f));
		label.AddThemeConstantOverride("outline_size", 2);
		label.SetTextAutoSize(text);
		button.AddChild(label);
	}

	private MonsterHexKind? GetMonsterHexBeforeRemovalSlot(int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < _monsterHexBeforeRemoval.Count
			? _monsterHexBeforeRemoval[slotIndex]
			: null;
	}
}
