using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private static string DetermineRarityKey(IReadOnlyList<RelicModel> relics, HextechSelectionMetadataMode metadataMode)
	{
		if (relics.Count == 0)
		{
			return "GOLD";
		}

		if (metadataMode == HextechSelectionMetadataMode.Forge
			&& HextechCatalog.TryGetForgeRarity(relics[0], out HextechRarityTier forgeRarity))
		{
			return GetRarityKey(forgeRarity);
		}

		return HextechCatalog.TryGetPlayerRuneRarity(relics[0], out HextechRarityTier runeRarity)
			? GetRarityKey(runeRarity)
			: "GOLD";
	}

	private static string GetRarityKey(HextechRarityTier rarity)
	{
		return rarity switch
		{
			HextechRarityTier.Silver => "SILVER",
			HextechRarityTier.Prismatic => "PRISMATIC",
			_ => "GOLD"
		};
	}

	internal static string DetermineCardRarityKey(
		RelicModel relic,
		HextechSelectionMetadataMode metadataMode)
	{
		if (metadataMode == HextechSelectionMetadataMode.Forge
			&& HextechCatalog.TryGetForgeRarity(relic, out HextechRarityTier forgeRarity))
		{
			return GetRarityKey(forgeRarity);
		}

		return HextechCatalog.TryGetPlayerRuneRarity(relic, out HextechRarityTier runeRarity)
			? GetRarityKey(runeRarity)
			: "GOLD";
	}

	private Color GetAccentColor()
	{
		return GetAccentColor(_rarityKey);
	}

	private static Color GetAccentColor(string rarityKey)
	{
		return rarityKey switch
		{
			"SILVER" => new Color(0.56f, 0.85f, 0.92f),
			"PRISMATIC" => new Color(0.94f, 0.43f, 1f),
			_ => new Color(0.94f, 0.76f, 0.35f)
		};
	}

	private string? GetCardFramePath()
	{
		return GetCardFramePath(_rarityKey);
	}

	private static string? GetCardFramePath(string rarityKey)
	{
		return rarityKey switch
		{
			"SILVER" => SilverCardFramePath,
			"PRISMATIC" => PrismaticCardFramePath,
			"GOLD" => GoldCardFramePath,
			_ => null
		};
	}

	private Texture2D? GetCardFrameTexture()
	{
		return GetCardFrameTexture(_rarityKey);
	}

	private static Texture2D? GetCardFrameTexture(string rarityKey)
	{
		string? path = GetCardFramePath(rarityKey);
		if (path == null)
		{
			return null;
		}

		Texture2D? texture = HextechTextures.LoadUiTexture(path);
		if (texture == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.GetCardFrameTexture: failed to load frame path={path}");
		}
		return texture;
	}

	private static readonly HashSet<string> DisplayTextureFallbackWarnings = new(StringComparer.Ordinal);

	private static Texture2D? GetDisplayTexture(RelicModel relic)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		try
		{
			Texture2D? bigIcon = relic.BigIcon;
			if (HextechTextures.IsTextureUsable(bigIcon))
			{
				return bigIcon;
			}

			Texture2D? icon = relic.Icon;
			if (HextechTextures.IsTextureUsable(icon))
			{
				return icon;
			}
		}
		catch (Exception ex)
		{
			WarnDisplayTextureFallbackOnce(id, ex.GetType().Name);
			return HextechTextures.GetMissingTexture();
		}

		WarnDisplayTextureFallbackOnce(id, "no usable icon");
		return HextechTextures.GetMissingTexture();
	}

	private static void WarnDisplayTextureFallbackOnce(ModelId id, string reason)
	{
		string key = id.ToString();
		if (DisplayTextureFallbackWarnings.Add(key))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.GetDisplayTexture: using fallback id={key} reason={reason}");
		}
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

	private static TextureRect CreateCardFrameOverlay(Texture2D texture)
	{
		float frameSide = PlayerRuneCardSize.Y;
		TextureRect frame = new()
		{
			Name = "RarityFrame",
			MouseFilter = MouseFilterEnum.Ignore,
			Texture = texture,
			CustomMinimumSize = new Vector2(frameSide, frameSide),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale
		};
		frame.AnchorLeft = 0.5f;
		frame.AnchorRight = 0.5f;
		frame.AnchorTop = 0f;
		frame.AnchorBottom = 0f;
		frame.OffsetLeft = -frameSide / 2f;
		frame.OffsetRight = frameSide / 2f;
		frame.OffsetTop = 0f;
		frame.OffsetBottom = frameSide;
		return frame;
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

	private static void ApplyRerollButtonVisualState(Button button, TextureRect icon, bool alreadyRerolled, bool hovered)
	{
		string path = alreadyRerolled
			? RerollButtonUsedTexturePath
			: hovered ? RerollButtonHoverTexturePath : RerollButtonTexturePath;
		icon.Texture = HextechTextures.LoadUiTexture(path) ?? HextechTextures.LoadUiTexture(RerollButtonTexturePath);
		button.Modulate = Colors.White;
		icon.SelfModulate = Colors.White;
	}

	private static StyleBoxFlat CreatePillStyle(Color accent)
	{
		Color background = accent.Lightened(0.24f);
		background.A = 0.78f;
		Color border = accent.Lightened(0.34f);
		border.A = 0.72f;

		StyleBoxFlat style = new();
		style.BgColor = background;
		style.BorderColor = border;
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(7);
		style.ContentMarginLeft = 8;
		style.ContentMarginRight = 8;
		style.ContentMarginTop = 3;
		style.ContentMarginBottom = 3;
		return style;
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
