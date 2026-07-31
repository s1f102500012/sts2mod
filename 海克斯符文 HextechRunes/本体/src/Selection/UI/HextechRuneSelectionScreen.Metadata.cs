using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen
{
	private Control CreateRarityPill()
	{
		return CreateRarityPill(_rarityKey);
	}

	private static Control CreateRarityPill(string rarityKey)
	{
		return CreateTextPill(
			new LocString(LocTable, "HEXTECH_SERIES." + rarityKey).GetRawText(),
			GetAccentColor(rarityKey));
	}

	private static Control CreatePlayerPoolPill(RelicModel relic, Color accent)
	{
		string poolKey = HextechCatalog.GetPlayerRunePoolKey(relic);
		return CreateTextPill(new LocString(LocTable, "HEXTECH_POOL." + poolKey).GetRawText(), accent);
	}

	private static Control CreatePlayerTagPill(RelicModel relic, Color accent)
	{
		string tagKey = HextechCatalog.GetPlayerRuneTagKey(relic);
		return CreateTextPill(new LocString(LocTable, "HEXTECH_TAG." + tagKey).GetRawText(), accent);
	}

	private Control CreatePlayerMetadataPills(RelicModel relic, string rarityKey)
	{
		Color accent = GetAccentColor(rarityKey);
		Control wrapper = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0f, 24f)
		};

		CenterContainer pillCenter = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		pillCenter.AnchorLeft = 0f;
		pillCenter.AnchorRight = 1f;
		pillCenter.AnchorTop = 0f;
		pillCenter.AnchorBottom = 1f;
		pillCenter.OffsetTop = -4f;
		pillCenter.OffsetBottom = -4f;

		HBoxContainer row = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		row.AddThemeConstantOverride("separation", 6);
		if (_metadataMode == HextechSelectionMetadataMode.Forge)
		{
			row.AddChild(CreateTextPill(new LocString(LocTable, "HEXTECH_POOL.FORGE").GetRawText(), accent));
			row.AddChild(CreateRarityPill(rarityKey));
		}
		else
		{
			row.AddChild(CreatePlayerPoolPill(relic, accent));
			row.AddChild(CreatePlayerTagPill(relic, accent));
		}
		pillCenter.AddChild(row);
		wrapper.AddChild(pillCenter);
		return wrapper;
	}

	private Control CreateTextPill(string text)
	{
		return CreateTextPill(text, GetAccentColor());
	}

	private static Control CreateTextPill(string text, Color accent)
	{
		PanelContainer pill = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		pill.AddThemeStyleboxOverride("panel", CreatePillStyle(accent));

		MegaLabel label = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			MinFontSize = 14,
			MaxFontSize = 14
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(label);
		label.AddThemeFontSizeOverride("font_size", 14);
		Color textColor = new(0.08f, 0.09f, 0.11f, 0.96f);
		label.AddThemeColorOverride("font_color", textColor);
		label.AddThemeColorOverride("font_outline_color", textColor);
		label.AddThemeConstantOverride("outline_size", 1);
		label.SetTextAutoSize(text);
		pill.AddChild(label);
		return pill;
	}
}
