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
	private static List<RuneConfigEntry> BuildRuneEntries()
	{
		List<RuneConfigEntry> entries = [];
		foreach (Type runeType in HextechCatalog.GetAllConfigurableRuneTypes())
		{
			RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(runeType));
			ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
			HextechRarityTier rarity = GetRuneRarity(runeType);
			string rarityKey = rarity.ToString().ToUpperInvariant();
			string poolKey = HextechCatalog.GetPlayerRunePoolKey(relic);
			string tagKey = HextechCatalog.GetPlayerRuneTagKey(relic);
			string sourceKey = GetConfigSourceKey(id);
			string sourceText = GetConfigSourceText(id);
			entries.Add(new RuneConfigEntry(
				id.Entry,
				relic,
				relic.Title.GetFormattedText(),
				new LocString(LocTable, "HEXTECH_SERIES." + rarityKey).GetRawText(),
				new LocString(LocTable, "HEXTECH_POOL." + poolKey).GetRawText(),
				new LocString(LocTable, "HEXTECH_TAG." + tagKey).GetRawText(),
				(int)rarity,
				poolKey,
				tagKey,
				sourceKey,
				sourceText));
		}

		return entries
			.OrderBy(static entry => entry.RarityOrder)
			.ThenBy(static entry => entry.SourceKey, StringComparer.Ordinal)
			.ThenBy(static entry => entry.PoolKey, StringComparer.Ordinal)
			.ThenBy(static entry => entry.TagKey, StringComparer.Ordinal)
			.ThenBy(static entry => entry.Title, StringComparer.CurrentCulture)
			.ToList();
	}

	private static List<RuneConfigEntry> BuildEnemyHexEntries()
	{
		List<RuneConfigEntry> entries = [];
		foreach (MonsterHexKind kind in Enum.GetValues<HextechRarityTier>()
			.SelectMany(MonsterHexCatalog.GetMonsterHexesForRarity))
		{
			RelicModel relic = MonsterHexCatalog.GetIconRelicForMonsterHex(kind);
			HextechRarityTier rarity = MonsterHexCatalog.GetMonsterHexRarity(kind);
			string rarityKey = rarity.ToString().ToUpperInvariant();
			entries.Add(new RuneConfigEntry(
				kind.ToString(),
				relic,
				relic.Title.GetFormattedText(),
				new LocString(LocTable, "HEXTECH_SERIES." + rarityKey).GetRawText(),
				L("HEXTECH_ENEMY_POOL_TITLE"),
				string.Empty,
				(int)rarity,
				"ENEMY",
				kind.ToString(),
				BaseConfigSourceKey,
				L("HEXTECH_CONFIG_SOURCE_BASE")));
		}

		return entries
			.OrderBy(static entry => entry.RarityOrder)
			.ThenBy(static entry => entry.Title, StringComparer.CurrentCulture)
			.ToList();
	}

	private static List<RuneConfigEntry> BuildForgeEntries()
	{
		List<RuneConfigEntry> entries = [];
		foreach (Type forgeType in HextechCatalog.GetAllForgeTypes())
		{
			RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(forgeType));
			ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
			HextechRarityTier rarity = HextechCatalog.TryGetForgeRarity(relic, out HextechRarityTier resolvedRarity)
				? resolvedRarity
				: HextechRarityTier.Gold;
			string rarityKey = rarity.ToString().ToUpperInvariant();
			string sourceKey = GetConfigSourceKey(id);
			string sourceText = GetConfigSourceText(id);
			entries.Add(new RuneConfigEntry(
				id.Entry,
				relic,
				relic.Title.GetFormattedText(),
				new LocString(LocTable, "HEXTECH_SERIES." + rarityKey).GetRawText(),
				L("HEXTECH_CONFIG_TAB_FORGES"),
				string.Empty,
				(int)rarity,
				"FORGE",
				forgeType.Name,
				sourceKey,
				sourceText));
		}

		return entries
			.OrderBy(static entry => entry.RarityOrder)
			.ThenBy(static entry => entry.SourceKey, StringComparer.Ordinal)
			.ThenBy(static entry => entry.Title, StringComparer.CurrentCulture)
			.ToList();
	}

	private static string GetConfigSourceKey(ModelId id)
	{
		string? assetModId = HextechExternalContentRegistry.GetAssetModId(id);
		return string.IsNullOrWhiteSpace(assetModId) || string.Equals(assetModId, ModInfo.Id, StringComparison.Ordinal)
			? BaseConfigSourceKey
			: ExternalConfigSourcePrefix + assetModId;
	}

	private static string GetConfigSourceText(ModelId id)
	{
		string? assetModId = HextechExternalContentRegistry.GetAssetModId(id);
		if (string.IsNullOrWhiteSpace(assetModId) || string.Equals(assetModId, ModInfo.Id, StringComparison.Ordinal))
		{
			return L("HEXTECH_CONFIG_SOURCE_BASE");
		}

		if (string.Equals(assetModId, SponsorPackModId, StringComparison.Ordinal))
		{
			return L("HEXTECH_CONFIG_SOURCE_EXTRA_PACK");
		}

		return string.Format(L("HEXTECH_CONFIG_SOURCE_EXTERNAL"), assetModId);
	}

	private static HextechRarityTier GetRuneRarity(Type runeType)
	{
		if (HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(HextechRarityTier.Silver).Contains(runeType))
		{
			return HextechRarityTier.Silver;
		}

		if (HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(HextechRarityTier.Prismatic).Contains(runeType))
		{
			return HextechRarityTier.Prismatic;
		}

		return HextechRarityTier.Gold;
	}

	private static void UpdateNumericLabels(IReadOnlyList<NumericValueBinding> bindings)
	{
		foreach (NumericValueBinding binding in bindings)
		{
			SetLabelText(binding.Number, binding.GetText());
		}
	}

	private static void UpdateBooleanToggles(IReadOnlyList<BooleanValueBinding> bindings)
	{
		foreach (BooleanValueBinding binding in bindings)
		{
			bool value = binding.GetValue();
			binding.Toggle.SetPressedNoSignal(value);
			binding.ApplyVisual?.Invoke(value);
		}
	}

	private static void UpdateSummary(
		Label summary,
		int pageIndex,
		IReadOnlySet<string> pendingDisabledPlayerIds,
		IReadOnlySet<string> pendingDisabledMonsterHexIds,
		IReadOnlySet<string> pendingDisabledForgeIds)
	{
		HashSet<string> configurableIds = HextechCatalog.GetConfigurablePlayerRuneIds()
			.Select(static id => id.Entry)
			.ToHashSet(StringComparer.Ordinal);
		int playerTotal = configurableIds.Count;
		int playerDisabled = pendingDisabledPlayerIds.Count(configurableIds.Contains);
		int playerEnabled = Math.Max(0, playerTotal - playerDisabled);
		int enemyTotal = Enum.GetValues<HextechRarityTier>()
			.SelectMany(MonsterHexCatalog.GetMonsterHexesForRarity)
			.Count();
		int enemyDisabled = pendingDisabledMonsterHexIds.Count;
		int enemyEnabled = Math.Max(0, enemyTotal - enemyDisabled);
		HashSet<string> forgeIds = HextechCatalog.GetAllForgeTypes()
			.Select(ModelDb.GetId)
			.Select(static id => id.Entry)
			.ToHashSet(StringComparer.Ordinal);
		int forgeTotal = forgeIds.Count;
		int forgeDisabled = pendingDisabledForgeIds.Count(forgeIds.Contains);
		int forgeEnabled = Math.Max(0, forgeTotal - forgeDisabled);
		string text = pageIndex switch
		{
			1 => $"{L("HEXTECH_PLAYER_POOL_TITLE")} {playerEnabled}/{playerTotal}  |  {L("HEXTECH_ENEMY_POOL_TITLE")} {enemyEnabled}/{enemyTotal}",
			2 => $"{L("HEXTECH_CONFIG_TAB_FORGES")} {forgeEnabled}/{forgeTotal}",
			_ => string.Empty
		};
		// Keep the summary line always present (even when empty) so the bottom bar height
		// stays constant across pages.
		SetLabelText(summary, text);
	}

	// 「杂项」页的配置分享区:导出/导入配置码 + 社区配置入口。动作由 CreateBottomBar 延迟填充。
	private static Control CreateShareSection(Action?[] shareActions, bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(L("HEXTECH_CONFIG_SHARE_TITLE"), null, compactLayout, out PanelContainer card);

		Label hint = CreateLabel(L("HEXTECH_CONFIG_SHARE_HINT"), 12, new Color(0.78f, 0.82f, 0.9f, 0.85f));
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		section.AddChild(hint);

		HBoxContainer buttons = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		buttons.AddThemeConstantOverride("separation", compactLayout ? 8 : 12);
		buttons.AddChild(CreateActionButton(L("HEXTECH_CONFIG_EXPORT_CODE"), () => shareActions[0]?.Invoke(), compactLayout));
		buttons.AddChild(CreateActionButton(L("HEXTECH_CONFIG_IMPORT_CODE"), () => shareActions[1]?.Invoke(), compactLayout));
		buttons.AddChild(CreateActionButton(L("HEXTECH_CONFIG_FEATURED"), () => shareActions[2]?.Invoke(), compactLayout));
		section.AddChild(buttons);

		return card;
	}

	private static Label CreateLabel(string text, int fontSize, Color color)
	{
		MegaLabel label = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			MinFontSize = fontSize,
			MaxFontSize = fontSize
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(label);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.Modulate = color;
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.68f));
		label.AddThemeConstantOverride("outline_size", 2);
		label.SetTextAutoSize(text);
		return label;
	}

	private static void SetLabelText(Label label, string text)
	{
		if (label is MegaLabel megaLabel)
		{
			megaLabel.SetTextAutoSize(text);
			return;
		}

		label.Text = text;
	}

	private static void AddCrispButtonText(Button button, string text, int fontSize, Color fontColor)
	{
		MegaLabel label = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MinFontSize = fontSize,
			MaxFontSize = fontSize
		};
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		HextechUiTheme.ApplyDefaultMegaLabelTheme(label);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", fontColor);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.62f));
		label.AddThemeConstantOverride("outline_size", 2);
		label.SetTextAutoSize(text);
		button.AddChild(label);
	}

	private static void StylePillTrack(Button toggle, float trackH)
	{
		int radius = (int)(trackH / 2f);
		// 轨道四态:关(深钢灰)/关悬停(略亮)/开(深金)/开悬停(更亮的金);旋钮颜色另由 panel stylebox 决定。
		toggle.AddThemeStyleboxOverride("normal", CreatePillTrackStyle(new Color(0.2f, 0.24f, 0.32f, 0.95f), new Color(0.46f, 0.55f, 0.68f, 0.5f), radius));
		toggle.AddThemeStyleboxOverride("hover", CreatePillTrackStyle(new Color(0.26f, 0.31f, 0.4f, 0.97f), new Color(0.62f, 0.7f, 0.82f, 0.66f), radius));
		toggle.AddThemeStyleboxOverride("pressed", CreatePillTrackStyle(new Color(0.86f, 0.66f, 0.28f, 0.98f), new Color(0.97f, 0.82f, 0.5f, 1f), radius));
		toggle.AddThemeStyleboxOverride("hover_pressed", CreatePillTrackStyle(new Color(0.94f, 0.74f, 0.34f, 1f), new Color(1f, 0.9f, 0.6f, 1f), radius));
		toggle.AddThemeStyleboxOverride("disabled", CreatePillTrackStyle(new Color(0.16f, 0.18f, 0.24f, 0.6f), new Color(0.34f, 0.38f, 0.46f, 0.4f), radius));
		toggle.AddThemeStyleboxOverride("focus", CreatePillFocusStyle(radius));
	}

	private static StyleBoxFlat CreatePillTrackStyle(Color background, Color border, int radius)
	{
		StyleBoxFlat style = new()
		{
			BgColor = background,
			BorderColor = border
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(radius);
		return style;
	}

	private static StyleBoxFlat CreatePillFocusStyle(int radius)
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0f, 0f, 0f, 0f),
			BorderColor = new Color(0.96f, 0.82f, 0.5f, 0.95f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(radius + 1);
		return style;
	}

	private static StyleBoxFlat CreatePillKnobStyle(float diameter)
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.97f, 0.95f, 0.88f, 1f),
			ShadowColor = new Color(0f, 0f, 0f, 0.35f),
			ShadowSize = 3,
			ShadowOffset = new Vector2(0f, 1f)
		};
		style.SetCornerRadiusAll((int)(diameter / 2f));
		return style;
	}

	private static StyleBoxFlat CreateButtonStyle(Color background, Color border)
	{
		StyleBoxFlat style = new()
		{
			BgColor = background,
			BorderColor = border,
			ShadowColor = new Color(0f, 0f, 0f, 0.24f),
			ShadowSize = 8,
			ShadowOffset = new Vector2(0f, 4f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		style.ContentMarginLeft = 12;
		style.ContentMarginRight = 12;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		return style;
	}

	private static StyleBoxFlat CreatePanelStyle()
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.055f, 0.07f, 0.1f, 0.96f),
			BorderColor = new Color(0.86f, 0.74f, 0.42f, 0.72f),
			ShadowColor = new Color(0f, 0f, 0f, 0.42f),
			ShadowSize = 28,
			ShadowOffset = new Vector2(0f, 12f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(18);
		return style;
	}

	private static Color GetRarityAccentColor(HextechRarityTier rarity)
	{
		return rarity switch
		{
			HextechRarityTier.Silver => new Color(0.56f, 0.85f, 0.92f),
			HextechRarityTier.Prismatic => new Color(0.94f, 0.43f, 1f),
			_ => new Color(0.94f, 0.76f, 0.35f)
		};
	}

	private static Color GetRarityAccentColorByOrder(int rarityOrder)
	{
		return rarityOrder switch
		{
			0 => GetRarityAccentColor(HextechRarityTier.Silver),
			2 => GetRarityAccentColor(HextechRarityTier.Prismatic),
			_ => GetRarityAccentColor(HextechRarityTier.Gold)
		};
	}

	private static StyleBoxFlat CreateCardStyle(Color? accent = null)
	{
		Color border = accent ?? new Color(0.48f, 0.55f, 0.66f, 0.34f);
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.09f, 0.11f, 0.16f, 0.55f),
			BorderColor = border,
			ShadowColor = new Color(0f, 0f, 0f, 0.22f),
			ShadowSize = 10,
			ShadowOffset = new Vector2(0f, 5f)
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(16);
		return style;
	}

	private static PanelContainer CreateCard(out MarginContainer body, Color? accent, bool compactLayout)
	{
		PanelContainer card = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		card.AddThemeStyleboxOverride("panel", CreateCardStyle(accent));

		body = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		int horizontal = compactLayout ? 14 : 20;
		int vertical = compactLayout ? 10 : 16;
		body.AddThemeConstantOverride("margin_left", horizontal);
		body.AddThemeConstantOverride("margin_right", horizontal);
		body.AddThemeConstantOverride("margin_top", vertical);
		body.AddThemeConstantOverride("margin_bottom", vertical);
		card.AddChild(body);
		return card;
	}

	private static VBoxContainer CreateCardSection(string title, Color? accent, bool compactLayout, out PanelContainer card)
	{
		card = CreateCard(out MarginContainer body, accent, compactLayout);
		VBoxContainer column = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		column.AddThemeConstantOverride("separation", compactLayout ? 8 : 12);
		body.AddChild(column);
		if (!string.IsNullOrEmpty(title))
		{
			column.AddChild(CreateSectionHeader(title, compactLayout ? 18 : 20));
		}

		return column;
	}

	private static void RemoveExistingOverlay(Node root)
	{
		if (root.GetNodeOrNull<Control>(OverlayName) is { } overlay && GodotObject.IsInstanceValid(overlay))
		{
			overlay.QueueFree();
		}
	}

	private static Node ResolveRoot(Node node)
	{
		return node.GetTree()?.Root is Node root ? root : node;
	}

	private static TNode? FindAncestor<TNode>(Node node)
		where TNode : Node
	{
		Node? current = node;
		while (current != null)
		{
			if (current is TNode match)
			{
				return match;
			}

			current = current.GetParent();
		}

		return null;
	}

	private static string L(string key)
	{
		try
		{
			return new LocString(LocTable, key).GetRawText();
		}
		catch
		{
			return key;
		}
	}
}
