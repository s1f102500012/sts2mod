using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen
{
	private Control CreateCardSlot(RelicModel relic, int slotIndex)
	{
		Control slot = new()
		{
			Name = $"Slot_{slotIndex}",
			CustomMinimumSize = PlayerRuneCardSize,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
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
			rerollButton.OffsetLeft = -PlayerRerollButtonSize.X / 2f;
			rerollButton.OffsetRight = PlayerRerollButtonSize.X / 2f;
			rerollButton.OffsetBottom = -PlayerRerollButtonBottomInset;
			rerollButton.OffsetTop = rerollButton.OffsetBottom - PlayerRerollButtonSize.Y;
			slot.AddChild(rerollButton);
			_rerollButtons.Add(rerollButton);
		}

		return slot;
	}

	private Button CreateCardButton(RelicModel relic)
	{
		string rarityKey = DetermineCardRarityKey(relic, _metadataMode);
		Color accent = GetAccentColor(rarityKey);
		Texture2D? cardFrameTexture = GetCardFrameTexture(rarityKey);
		bool useImageFrame = cardFrameTexture != null;
		Button button = new()
		{
			Name = $"{(relic.CanonicalInstance?.Id ?? relic.Id).Entry}_Card",
			CustomMinimumSize = PlayerRuneCardSize,
			Text = string.Empty,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			ClipContents = useImageFrame
		};
		Color transparentBorder = new(0f, 0f, 0f, 0f);
		button.AddThemeStyleboxOverride("normal", CreateCardStyle(new Color(0.08f, 0.1f, 0.14f, 0.74f), useImageFrame ? transparentBorder : accent.Lightened(0.08f), useImageFrame ? 0 : 2, 0.18f));
		button.AddThemeStyleboxOverride("hover", CreateCardStyle(new Color(0.1f, 0.12f, 0.18f, 0.84f), useImageFrame ? transparentBorder : accent, useImageFrame ? 0 : 4, 0.32f));
		button.AddThemeStyleboxOverride("pressed", CreateCardStyle(new Color(0.07f, 0.09f, 0.13f, 0.9f), useImageFrame ? transparentBorder : accent.Lightened(0.14f), useImageFrame ? 0 : 4, 0.24f));
		button.AddThemeStyleboxOverride("focus", CreateCardStyle(new Color(0.1f, 0.12f, 0.18f, 0.84f), useImageFrame ? transparentBorder : accent, useImageFrame ? 0 : 4, 0.32f));
		button.AddThemeStyleboxOverride("disabled", CreateCardStyle(new Color(0.08f, 0.09f, 0.12f, 0.62f), useImageFrame ? transparentBorder : accent.Darkened(0.4f), useImageFrame ? 0 : 2, 0.08f));

		if (cardFrameTexture != null)
		{
			button.AddChild(CreateCardFrameOverlay(cardFrameTexture));
		}

		MarginContainer margin = new();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", useImageFrame ? 40 : 22);
		margin.AddThemeConstantOverride("margin_right", useImageFrame ? 40 : 22);
		margin.AddThemeConstantOverride("margin_top", useImageFrame ? 32 : 22);
		margin.AddThemeConstantOverride("margin_bottom", PlayerRuneCardBottomMargin);
		button.AddChild(margin);

		VBoxContainer content = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		content.AddThemeConstantOverride("separation", 14);
		margin.AddChild(content);

		if (!useImageFrame)
		{
			ColorRect accentBar = new()
			{
				MouseFilter = MouseFilterEnum.Ignore,
				Color = accent,
				CustomMinimumSize = new Vector2(0f, 6f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			content.AddChild(accentBar);
		}

		CenterContainer iconBox = new()
		{
			CustomMinimumSize = new Vector2(0f, 176f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		content.AddChild(iconBox);
		TextureRect relicTexture = CreateRelicTexture(relic, 152f);
		iconBox.AddChild(relicTexture);

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 28,
			MinFontSize = 18
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(title);
		title.Modulate = new Color(0.98f, 0.97f, 0.92f, 0.97f);
		title.SetTextAutoSize(relic.Title.GetFormattedText());
		content.AddChild(title);

		content.AddChild(CreatePlayerMetadataPills(relic, rarityKey));

		MegaRichTextLabel body = CreateDescriptionLabel();
		string descriptionText = relic.DynamicDescription.GetFormattedText();
		// 附加小字提示(升级类海克斯的补卡说明):换行紧跟正文,小号淡色([gold]卡名由标签着色)。
		if (relic is IHextechSelectionFooterProvider footerProvider
			&& footerProvider.GetSelectionFooterText() is { Length: > 0 } footerText)
		{
			descriptionText += $"\n[font_size=13][color=#d8d4c090]{footerText}[/color][/font_size]";
		}

		body.SetTextAutoSize(descriptionText);
		content.AddChild(body);

		SetMouseFilterIgnoreRecursive(margin);
		AttachRelicHoverTips(button, relic);
		button.Pressed += () => OnHolderSelected(relic);
		return button;
	}

	private Button CreateRerollButton(int slotIndex)
	{
		bool rerollLimitReached = IsPlayerRuneRerollLimitReached(slotIndex);
		Button button = CreateRerollIconButton(
			$"RerollButton_{slotIndex}",
			PlayerRerollButtonSize,
			rerollLimitReached,
			includeGoldenVisual: true);
		button.Pressed += () => OnRerollPressed(slotIndex);
		return button;
	}

	private Button CreateRerollIconButton(string name, Vector2 size, bool disabled, bool includeGoldenVisual)
	{
		Button button = new()
		{
			Name = name,
			Text = string.Empty,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = size,
			Disabled = disabled
		};
		StyleBoxFlat transparentStyle = CreateRerollStyle(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
		transparentStyle.SetBorderWidthAll(0);
		transparentStyle.ShadowSize = 0;
		transparentStyle.ShadowColor = new Color(0f, 0f, 0f, 0f);
		button.AddThemeStyleboxOverride("normal", transparentStyle);
		button.AddThemeStyleboxOverride("hover", transparentStyle);
		button.AddThemeStyleboxOverride("pressed", transparentStyle);
		button.AddThemeStyleboxOverride("focus", transparentStyle);
		button.AddThemeStyleboxOverride("disabled", transparentStyle);

		TextureRect icon = new()
		{
			Name = "RerollButtonTexture",
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SelfModulate = Colors.White
		};
		icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		ApplyRerollButtonVisualState(button, icon, disabled, hovered: false);
		if (icon.Texture == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.CreateRerollButton: failed to load reroll button texture path={RerollButtonTexturePath}");
		}
		button.AddChild(icon);
		HextechGoldenRerollVisual? goldenVisual = includeGoldenVisual
			? CreateGoldenRerollVisual(disabled)
			: null;
		if (goldenVisual != null)
		{
			button.AddChild(goldenVisual);
			_goldenRerollVisuals.Add(goldenVisual);
		}
		bool hovered = false;
		bool focused = false;
		void UpdateVisualState()
		{
			bool highlighted = hovered || focused;
			ApplyRerollButtonVisualState(button, icon, disabled, highlighted);
			goldenVisual?.SetVisualState(
				_goldenRerollSession?.IsActive == true,
				highlighted,
				disabled);
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
		return button;
	}

	private HextechGoldenRerollVisual? CreateGoldenRerollVisual(bool rerollLimitReached)
	{
		if (_goldenRerollSession?.CanActivate != true)
		{
			return null;
		}

		Texture2D? outerMask = HextechTextures.LoadUiTexture(GoldenRerollOuterMaskPath);
		Texture2D? fillMask = HextechTextures.LoadUiTexture(GoldenRerollFillMaskPath);
		HextechGoldenRerollVisual? visual = HextechGoldenRerollVisual.Create(
			outerMask,
			fillMask,
			PlayerRerollButtonSize,
			GoldenRerollSourceScale);
		if (visual == null)
		{
			Log.Warn(
				$"[{ModInfo.Id}][Mayhem] SelectionScreen.CreateGoldenRerollVisual: " +
				$"failed to load masks outer={GoldenRerollOuterMaskPath} fill={GoldenRerollFillMaskPath}");
			return null;
		}

		visual.SetVisualState(
			_goldenRerollSession.IsActive,
			hovered: false,
			disabled: rerollLimitReached);
		return visual;
	}
}
