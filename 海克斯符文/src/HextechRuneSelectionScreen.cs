using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private const string LocTable = "relic_collection";
	private const string RerollIconPath = "res://HextechRunes/images/ui/hextechReroll.png";

	private readonly TaskCompletionSource<IEnumerable<RelicModel>> _completionSource = new();
	private readonly Func<IReadOnlyList<RelicModel>, int, IReadOnlyList<RelicModel>>? _rerollFunc;
	private List<RelicModel> _relics;
	private readonly RelicModel? _monsterHexRelic;
	private readonly string _rarityKey;
	private readonly List<Button> _holders = new();
	private readonly List<Button> _rerollButtons = new();
	private readonly List<bool> _rerolledSlots = new();
	private readonly List<int> _rerollHistory = new();
	private HBoxContainer? _cardsRow;
	private bool _choiceLocked;
	private bool _restoreAfterMapReopenQueued;

	public NetScreenType ScreenType => NetScreenType.Rewards;

	public bool UseSharedBackstop => true;

	public Control? DefaultFocusedControl => _holders.FirstOrDefault();

	public bool RequestedReroll => false;

	public IReadOnlyList<RelicModel> CurrentRelics => _relics;

	public IReadOnlyList<int> RerollHistory => _rerollHistory;

	private HextechRuneSelectionScreen(IReadOnlyList<RelicModel> relics, RelicModel? monsterHexRelic, Func<IReadOnlyList<RelicModel>, int, IReadOnlyList<RelicModel>>? rerollFunc)
	{
		_relics = relics.ToList();
		_monsterHexRelic = monsterHexRelic;
		_rerollFunc = rerollFunc;
		_rarityKey = DetermineRarityKey(relics);
		Name = nameof(HextechRuneSelectionScreen);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		Visible = true;
		BuildUi();
	}

	public static HextechRuneSelectionScreen Create(IReadOnlyList<RelicModel> relics, RelicModel? monsterHexRelic, Func<IReadOnlyList<RelicModel>, int, IReadOnlyList<RelicModel>>? rerollFunc = null)
	{
		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.Create: count={relics.Count}");
		return new HextechRuneSelectionScreen(relics, monsterHexRelic, rerollFunc);
	}

	public override void _ExitTree()
	{
		_completionSource.TrySetResult(Array.Empty<RelicModel>());
		base._ExitTree();
	}

	private void BuildUi()
	{
		ColorRect backdrop = new()
		{
			Name = "DimOverlay",
			Color = new Color(0.02f, 0.025f, 0.035f, 0.56f),
			MouseFilter = MouseFilterEnum.Stop
		};
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);

		CenterContainer screenCenter = new()
		{
			Name = "ScreenCenter",
			MouseFilter = MouseFilterEnum.Ignore
		};
		screenCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(screenCenter);

		PanelContainer contentPanel = new()
		{
			Name = "ContentPanel",
			CustomMinimumSize = new Vector2(1180f, 780f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		contentPanel.AddThemeStyleboxOverride("panel", CreateContentPanelStyle());
		screenCenter.AddChild(contentPanel);

		MarginContainer contentMargin = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		contentMargin.AddThemeConstantOverride("margin_left", 30);
		contentMargin.AddThemeConstantOverride("margin_right", 30);
		contentMargin.AddThemeConstantOverride("margin_top", 28);
		contentMargin.AddThemeConstantOverride("margin_bottom", 28);
		contentPanel.AddChild(contentMargin);

		VBoxContainer root = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		root.AddThemeConstantOverride("separation", 20);
		contentMargin.AddChild(root);

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 48,
			MinFontSize = 30
		};
		ApplyDefaultMegaLabelTheme(title);
		title.Modulate = new Color(0.96f, 0.97f, 0.99f, 0.98f);
		title.SetTextAutoSize(new LocString(LocTable, "HEXTECH_SELECTION_TITLE").GetRawText());
		root.AddChild(title);

		if (_monsterHexRelic != null)
		{
			root.AddChild(CreateEnemyPreview(_monsterHexRelic));
		}

		HBoxContainer row = new()
		{
			Name = "PlayerCardsRow",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 28);
		root.AddChild(row);
		_cardsRow = row;

		RebuildCards();

	}

	private void RebuildCards()
	{
		if (_cardsRow == null)
		{
			return;
		}

		foreach (Node child in _cardsRow.GetChildren())
		{
			_cardsRow.RemoveChild(child);
			child.QueueFree();
		}

		_holders.Clear();
		_rerollButtons.Clear();
		while (_rerolledSlots.Count < _relics.Count)
		{
			_rerolledSlots.Add(false);
		}

		for (int i = 0; i < _relics.Count; i++)
		{
			Control slot = CreateCardSlot(_relics[i], i);
			_cardsRow.AddChild(slot);
		}
	}

	private Control CreateEnemyPreview(RelicModel relic)
	{
		PanelContainer panel = new()
		{
			Name = "EnemyPreviewPanel",
			CustomMinimumSize = new Vector2(1040f, 148f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", CreatePreviewStyle());

		MarginContainer margin = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		panel.AddChild(margin);

		HBoxContainer row = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 18);
		margin.AddChild(row);

		CenterContainer iconBox = new()
		{
			CustomMinimumSize = new Vector2(96f, 96f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddChild(iconBox);
		iconBox.AddChild(CreateRelicTexture(relic, 84f));

		VBoxContainer textColumn = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		textColumn.AddThemeConstantOverride("separation", 5);
		row.AddChild(textColumn);

		MegaLabel eyebrow = new()
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			MaxFontSize = 15,
			MinFontSize = 12
		};
		ApplyDefaultMegaLabelTheme(eyebrow);
		eyebrow.Modulate = new Color(0.81f, 0.86f, 0.91f, 0.72f);
		eyebrow.SetTextAutoSize(new LocString(LocTable, "HEXTECH_ENEMY_PREVIEW_LABEL").GetRawText());
		textColumn.AddChild(eyebrow);

		HBoxContainer titleRow = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		titleRow.AddThemeConstantOverride("separation", 10);
		textColumn.AddChild(titleRow);

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			MaxFontSize = 32,
			MinFontSize = 24
		};
		ApplyDefaultMegaLabelTheme(title);
		title.Modulate = new Color(0.97f, 0.96f, 0.9f, 0.96f);
		title.SetTextAutoSize(relic.Title.GetFormattedText());
		titleRow.AddChild(title);

		titleRow.AddChild(CreateRarityPill());

		MegaRichTextLabel body = CreateDescriptionLabel();
		body.MaxFontSize = 17;
		body.MinFontSize = 13;
		if (ModInfo.TryGetMonsterHexKind(relic, out MonsterHexKind enemyHex))
		{
			body.SetTextAutoSize(ModInfo.GetEnemyHexDescriptionFormatted(enemyHex));
		}
		else
		{
			body.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
		}
		textColumn.AddChild(body);

		return panel;
	}

	private Control CreateCardSlot(RelicModel relic, int slotIndex)
	{
		Control slot = new()
		{
			Name = $"Slot_{slotIndex}",
			CustomMinimumSize = new Vector2(344f, 552f),
			MouseFilter = MouseFilterEnum.Ignore
		};

		Button button = CreateCardButton(relic);
		button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		slot.AddChild(button);
		_holders.Add(button);

		if (_rerollFunc != null)
		{
			Button rerollButton = CreateRerollButton(slotIndex);
			rerollButton.AnchorLeft = 0.5f;
			rerollButton.AnchorRight = 0.5f;
			rerollButton.AnchorTop = 1f;
			rerollButton.AnchorBottom = 1f;
			rerollButton.OffsetLeft = -56f;
			rerollButton.OffsetRight = 56f;
			rerollButton.OffsetTop = -82f;
			rerollButton.OffsetBottom = -26f;
			slot.AddChild(rerollButton);
			_rerollButtons.Add(rerollButton);
		}

		return slot;
	}

	private Button CreateCardButton(RelicModel relic)
	{
		Color accent = GetAccentColor();
		Button button = new()
		{
			Name = $"{(relic.CanonicalInstance?.Id ?? relic.Id).Entry}_Card",
			CustomMinimumSize = new Vector2(344f, 552f),
			Text = string.Empty,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		button.AddThemeStyleboxOverride("normal", CreateCardStyle(new Color(0.08f, 0.1f, 0.14f, 0.74f), accent.Lightened(0.08f), 2, 0.18f));
		button.AddThemeStyleboxOverride("hover", CreateCardStyle(new Color(0.1f, 0.12f, 0.18f, 0.84f), accent, 4, 0.32f));
		button.AddThemeStyleboxOverride("pressed", CreateCardStyle(new Color(0.07f, 0.09f, 0.13f, 0.9f), accent.Lightened(0.14f), 4, 0.24f));
		button.AddThemeStyleboxOverride("focus", CreateCardStyle(new Color(0.1f, 0.12f, 0.18f, 0.84f), accent, 4, 0.32f));
		button.AddThemeStyleboxOverride("disabled", CreateCardStyle(new Color(0.08f, 0.09f, 0.12f, 0.62f), accent.Darkened(0.4f), 2, 0.08f));

		MarginContainer margin = new();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_top", 22);
		margin.AddThemeConstantOverride("margin_bottom", 84);
		button.AddChild(margin);

		VBoxContainer content = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		content.AddThemeConstantOverride("separation", 14);
		margin.AddChild(content);

		ColorRect accentBar = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Color = accent,
			CustomMinimumSize = new Vector2(0f, 6f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		content.AddChild(accentBar);

		CenterContainer iconBox = new()
		{
			CustomMinimumSize = new Vector2(0f, 176f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		content.AddChild(iconBox);
		iconBox.AddChild(CreateRelicTexture(relic, 152f));

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 28,
			MinFontSize = 18
		};
		ApplyDefaultMegaLabelTheme(title);
		title.Modulate = new Color(0.98f, 0.97f, 0.92f, 0.97f);
		title.SetTextAutoSize(relic.Title.GetFormattedText());
		content.AddChild(title);

		CenterContainer pillCenter = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		pillCenter.AddChild(CreateRarityPill());
		content.AddChild(pillCenter);

		MegaRichTextLabel body = CreateDescriptionLabel();
		body.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
		content.AddChild(body);

		SetMouseFilterIgnoreRecursive(margin);
		button.Pressed += () => OnHolderSelected(relic);
		return button;
	}

	private Button CreateRerollButton(int slotIndex)
	{
		bool alreadyRerolled = _rerolledSlots.ElementAtOrDefault(slotIndex);
		Button button = new()
		{
			Name = $"RerollButton_{slotIndex}",
			Text = string.Empty,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(112f, 56f),
			Disabled = alreadyRerolled
		};
		Color accent = GetAccentColor();
		button.AddThemeStyleboxOverride("normal", CreateRerollStyle(new Color(0.08f, 0.1f, 0.15f, 0.72f), accent.Lightened(0.05f)));
		button.AddThemeStyleboxOverride("hover", CreateRerollStyle(new Color(0.1f, 0.13f, 0.18f, 0.82f), accent));
		button.AddThemeStyleboxOverride("pressed", CreateRerollStyle(new Color(0.07f, 0.09f, 0.13f, 0.86f), accent.Lightened(0.12f)));
		button.AddThemeStyleboxOverride("focus", CreateRerollStyle(new Color(0.1f, 0.13f, 0.18f, 0.82f), accent));
		button.AddThemeStyleboxOverride("disabled", CreateRerollStyle(new Color(0.08f, 0.09f, 0.12f, 0.56f), accent.Darkened(0.35f)));

		TextureRect icon = new()
		{
			Name = "RerollIcon",
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(36f, 36f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SelfModulate = Colors.White
		};
		icon.AnchorLeft = 0.5f;
		icon.AnchorRight = 0.5f;
		icon.AnchorTop = 0.5f;
		icon.AnchorBottom = 0.5f;
		icon.OffsetLeft = -18f;
		icon.OffsetRight = 18f;
		icon.OffsetTop = -18f;
		icon.OffsetBottom = 18f;
		icon.Texture = AssetHooks.LoadUiTexture(RerollIconPath);
		if (icon.Texture == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.CreateRerollButton: failed to load reroll icon path={RerollIconPath}");
		}
		ApplyRerollButtonVisualState(button, icon, alreadyRerolled);
		button.AddChild(icon);
		button.Pressed += () => OnRerollPressed(slotIndex);
		return button;
	}

	private TextureRect CreateRelicTexture(RelicModel relic, float sideLength)
	{
		TextureRect textureRect = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Texture = GetDisplayTexture(relic),
			CustomMinimumSize = new Vector2(sideLength, sideLength),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		return textureRect;
	}

	private MegaRichTextLabel CreateDescriptionLabel()
	{
		MegaRichTextLabel body = new()
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MaxFontSize = 20,
			MinFontSize = 15,
			BbcodeEnabled = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ApplyDefaultMegaRichTextTheme(body);
		body.AddThemeColorOverride("default_color", new Color(0.9f, 0.93f, 0.97f, 0.92f));
		return body;
	}

	private Control CreateRarityPill()
	{
		PanelContainer pill = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		pill.AddThemeStyleboxOverride("panel", CreatePillStyle(GetAccentColor()));

		Label label = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Text = new LocString(LocTable, "HEXTECH_SERIES." + _rarityKey).GetRawText(),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeColorOverride("font_color", new Color(0.08f, 0.09f, 0.11f, 0.92f));
		pill.AddChild(label);
		return pill;
	}

	private void OnHolderSelected(RelicModel relic)
	{
		if (_choiceLocked)
		{
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

		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnHolderSelected: relic={(relic.CanonicalInstance?.Id ?? relic.Id).Entry}");
		GetViewport()?.SetInputAsHandled();
		_completionSource.TrySetResult([relic]);
	}

	private void OnRerollPressed(int slotIndex)
	{
		if (_choiceLocked || _rerollFunc == null || _rerolledSlots.ElementAtOrDefault(slotIndex))
		{
			return;
		}

		IReadOnlyList<RelicModel> rerolled = _rerollFunc(_relics, slotIndex);
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

		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.OnRerollPressed: slot={slotIndex} old={oldRelic} new={newRelic}");
		_relics = rerolled.ToList();
		_rerolledSlots[slotIndex] = true;
		_rerollHistory.Add(slotIndex);
		RebuildCards();
	}

	public async Task<IEnumerable<RelicModel>> RelicsSelected()
	{
		IEnumerable<RelicModel> result = await _completionSource.Task;
		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RelicsSelected: begin dismiss mousePressed={Input.IsMouseButtonPressed(MouseButton.Left)}");
		if (IsInsideTree())
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			while (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RelicsSelected: removing overlay");
		NOverlayStack.Instance?.Remove(this);
		return result;
	}

	public void AfterOverlayOpened()
	{
		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayOpened");
		Modulate = Colors.White;
		Visible = true;
		CallDeferred(nameof(GrabFocus));
	}

	public void AfterOverlayClosed()
	{
		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayClosed");
		QueueFree();
	}

	public void AfterOverlayShown()
	{
		Visible = true;
		CallDeferred(nameof(GrabFocus));
	}

	public void AfterOverlayHidden()
	{
		Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.AfterOverlayHidden: choiceLocked={_choiceLocked} capstoneOpen={NCapstoneContainer.Instance?.InUse == true} mapOpen={NMapScreen.Instance?.IsOpen == true}");
		Visible = false;
		if (!_choiceLocked && !_restoreAfterMapReopenQueued && IsInsideTree())
		{
			_restoreAfterMapReopenQueued = true;
			_ = TaskHelper.RunSafely(RestoreAfterMapReopenAsync());
		}
	}

	private async Task RestoreAfterMapReopenAsync()
	{
		try
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInsideTree() || _choiceLocked)
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

			Log.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.RestoreAfterMapReopen: closing map reopened over unresolved selection");
			NMapScreen.Instance?.Close(animateOut: false);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			NOverlayStack.Instance?.ShowOverlays();
		}
		finally
		{
			_restoreAfterMapReopenQueued = false;
		}
	}

	private static string DetermineRarityKey(IReadOnlyList<RelicModel> relics)
	{
		if (relics.Count == 0)
		{
			return "GOLD";
		}

		Type? relicType = ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Silver).FirstOrDefault(type => ModelDb.GetId(type) == (relics[0].CanonicalInstance?.Id ?? relics[0].Id));
		if (relicType != null)
		{
			return "SILVER";
		}

		relicType = ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Prismatic).FirstOrDefault(type => ModelDb.GetId(type) == (relics[0].CanonicalInstance?.Id ?? relics[0].Id));
		return relicType != null ? "PRISMATIC" : "GOLD";
	}

	private Color GetAccentColor()
	{
		return _rarityKey switch
		{
			"SILVER" => new Color(0.56f, 0.85f, 0.92f),
			"PRISMATIC" => new Color(0.94f, 0.43f, 1f),
			_ => new Color(0.94f, 0.76f, 0.35f)
		};
	}

	private static Texture2D? GetDisplayTexture(RelicModel relic)
	{
		return relic.BigIcon ?? relic.Icon;
	}

	private static StyleBoxFlat CreateContentPanelStyle()
	{
		StyleBoxFlat style = new();
		style.BgColor = new Color(0.04f, 0.05f, 0.08f, 0.4f);
		style.BorderColor = new Color(0.48f, 0.55f, 0.66f, 0.35f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(28);
		style.ContentMarginLeft = 8;
		style.ContentMarginRight = 8;
		style.ContentMarginTop = 8;
		style.ContentMarginBottom = 8;
		style.ShadowColor = new Color(0f, 0f, 0f, 0.26f);
		style.ShadowSize = 18;
		style.ShadowOffset = new Vector2(0f, 10f);
		return style;
	}

	private static StyleBoxFlat CreatePreviewStyle()
	{
		StyleBoxFlat style = new();
		style.BgColor = new Color(0.07f, 0.09f, 0.13f, 0.48f);
		style.BorderColor = new Color(0.72f, 0.42f, 0.42f, 0.55f);
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(20);
		style.ContentMarginLeft = 6;
		style.ContentMarginRight = 6;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		style.ShadowColor = new Color(0f, 0f, 0f, 0.16f);
		style.ShadowSize = 10;
		style.ShadowOffset = new Vector2(0f, 6f);
		return style;
	}

	private static StyleBoxFlat CreateCardStyle(Color background, Color border, int borderWidth, float shadowAlpha)
	{
		StyleBoxFlat style = new();
		style.BgColor = background;
		style.BorderColor = border;
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(26);
		style.ContentMarginLeft = 6;
		style.ContentMarginRight = 6;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		style.ShadowColor = new Color(0f, 0f, 0f, shadowAlpha);
		style.ShadowSize = 16;
		style.ShadowOffset = new Vector2(0f, 10f);
		return style;
	}

	private static StyleBoxFlat CreateRerollStyle(Color background, Color border)
	{
		StyleBoxFlat style = new();
		style.BgColor = background;
		style.BorderColor = border;
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(18);
		style.ContentMarginLeft = 6;
		style.ContentMarginRight = 6;
		style.ContentMarginTop = 4;
		style.ContentMarginBottom = 4;
		style.ShadowColor = new Color(0f, 0f, 0f, 0.16f);
		style.ShadowSize = 8;
		style.ShadowOffset = new Vector2(0f, 4f);
		return style;
	}

	private static void ApplyRerollButtonVisualState(Button button, TextureRect icon, bool alreadyRerolled)
	{
		if (!alreadyRerolled)
		{
			button.Modulate = Colors.White;
			icon.SelfModulate = Colors.White;
			return;
		}

		button.Modulate = new Color(0.62f, 0.64f, 0.68f, 0.82f);
		icon.SelfModulate = new Color(0.46f, 0.49f, 0.54f, 0.95f);
	}

	private static StyleBoxFlat CreatePillStyle(Color accent)
	{
		StyleBoxFlat style = new();
		style.BgColor = accent;
		style.BorderColor = accent.Lightened(0.15f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(12);
		style.ContentMarginLeft = 12;
		style.ContentMarginRight = 12;
		style.ContentMarginTop = 5;
		style.ContentMarginBottom = 5;
		return style;
	}

	private static void ApplyDefaultMegaLabelTheme(MegaLabel label)
	{
		Font font = label.GetThemeDefaultFont();
		if (font != null)
		{
			label.AddThemeFontOverride("font", font);
		}

		int fontSize = label.GetThemeDefaultFontSize();
		if (fontSize > 0)
		{
			label.AddThemeFontSizeOverride("font_size", fontSize);
		}
	}

	private static void ApplyDefaultMegaRichTextTheme(MegaRichTextLabel label)
	{
		Font font = label.GetThemeDefaultFont();
		if (font != null)
		{
			label.AddThemeFontOverride("normal_font", font);
			label.AddThemeFontOverride("bold_font", font);
			label.AddThemeFontOverride("italics_font", font);
			label.AddThemeFontOverride("bold_italics_font", font);
			label.AddThemeFontOverride("mono_font", font);
		}

		int fontSize = label.GetThemeDefaultFontSize();
		if (fontSize > 0)
		{
			label.AddThemeFontSizeOverride("normal_font_size", fontSize);
			label.AddThemeFontSizeOverride("bold_font_size", fontSize);
			label.AddThemeFontSizeOverride("italics_font_size", fontSize);
			label.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
			label.AddThemeFontSizeOverride("mono_font_size", fontSize);
		}
	}

	private static void SetMouseFilterIgnoreRecursive(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Control control)
			{
				control.MouseFilter = MouseFilterEnum.Ignore;
			}

			SetMouseFilterIgnoreRecursive(child);
		}
	}
}
