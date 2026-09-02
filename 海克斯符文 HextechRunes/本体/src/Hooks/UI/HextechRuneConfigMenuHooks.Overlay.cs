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
	private const string OpenerMetaKey = "hextech_opener";

	private static void OpenOverlay(Node source)
	{
		Node root = ResolveRoot(source);
		RemoveExistingOverlay(root);
		Control overlay = CreateOverlay(out RuneConfigOverlayState state);
		if (source is Control opener)
		{
			overlay.SetMeta(OpenerMetaKey, opener);
		}

		root.AddChild(overlay);
		if (overlay is HextechControllerOverlay controllerOverlay)
		{
			controllerOverlay.InitialFocus = state.InitialFocus;
		}
		ConfigureHorizontalFocus(state.TabButtons);
		WireControllerFocusScrolling(overlay);
		TaskHelper.RunSafely(PopulateRuneIconsAsync(overlay, state));
		TaskHelper.RunSafely(AnimateOverlayInAsync(overlay));
	}

	private static async Task AnimateOverlayInAsync(Control overlay)
	{
		if (!await HextechGodotAsync.AwaitProcessFrameAsync(overlay))
		{
			return;
		}

		overlay.Modulate = new Color(1f, 1f, 1f, 0f);
		Control? panel = overlay.GetNodeOrNull<Control>(ConfigPanelName);
		if (panel != null)
		{
			panel.PivotOffset = panel.Size * 0.5f;
			panel.Scale = Vector2.One * OverlayOpenScale;
		}

		Tween tween = overlay.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(overlay, "modulate:a", 1f, OverlayOpenSeconds).SetEase(Tween.EaseType.Out);
		if (panel != null)
		{
			tween.TweenProperty(panel, "scale", Vector2.One, OverlayOpenSeconds)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Back);
		}
	}

	private static void CloseOverlayAnimated(Control overlay)
	{
		if (!GodotObject.IsInstanceValid(overlay))
		{
			return;
		}

		// Guard against double-trigger (e.g. save + cancel in quick succession).
		if (overlay.HasMeta("hextech_closing"))
		{
			return;
		}

		overlay.SetMeta("hextech_closing", true);
		overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
		Control? panel = overlay.GetNodeOrNull<Control>(ConfigPanelName);
		if (panel != null)
		{
			panel.PivotOffset = panel.Size * 0.5f;
		}

		Tween tween = overlay.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(overlay, "modulate:a", 0f, OverlayCloseSeconds).SetEase(Tween.EaseType.In);
		if (panel != null)
		{
			tween.TweenProperty(panel, "scale", Vector2.One * OverlayOpenScale, OverlayCloseSeconds).SetEase(Tween.EaseType.In);
		}

		Control? opener = overlay.HasMeta(OpenerMetaKey) ? overlay.GetMeta(OpenerMetaKey).As<Control>() : null;
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(overlay))
			{
				overlay.QueueFree();
			}

			// 覆盖层不是原版 SubmenuStack 的一员,焦点要自己还给打开它的按钮。
			if (opener != null && GodotObject.IsInstanceValid(opener) && opener.IsInsideTree() && opener.IsVisibleInTree())
			{
				opener.GrabFocus();
			}
		}));
	}

	private static Control CreateOverlay(out RuneConfigOverlayState state)
	{
		HextechControllerOverlay overlay = new()
		{
			Name = OverlayName,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.All,
			FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Enabled,
			ZIndex = OverlayZIndex
		};
		overlay.CancelRequested = () => CloseWithoutSaving(overlay);
		overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		ColorRect shade = new()
		{
			Color = new Color(0f, 0f, 0f, 0.72f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(shade);

		CenterContainer center = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(center);

		bool compactLayout = IsCompactConfigLayout();
		PanelContainer panel = new()
		{
			Name = ConfigPanelName,
			CustomMinimumSize = GetResponsivePanelSize(),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		center.AddChild(panel);

		MarginContainer margin = new();
		margin.AddThemeConstantOverride("margin_left", compactLayout ? 20 : 28);
		margin.AddThemeConstantOverride("margin_right", compactLayout ? 20 : 28);
		margin.AddThemeConstantOverride("margin_top", compactLayout ? 16 : 24);
		margin.AddThemeConstantOverride("margin_bottom", compactLayout ? 16 : 24);
		panel.AddChild(margin);

		VBoxContainer content = new()
		{
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		content.AddThemeConstantOverride("separation", compactLayout ? 8 : 14);
		margin.AddChild(content);

		Label title = CreateLabel(L("HEXTECH_CONFIG_TITLE"), compactLayout ? 26 : 30, new Color(0.98f, 0.94f, 0.82f, 1f));
		title.HorizontalAlignment = HorizontalAlignment.Center;
		content.AddChild(title);

		HextechRunConfigurationSnapshot pendingSnapshot = HextechRuneConfiguration.GetSnapshot();
		int[] pendingPlayerHexCounts = pendingSnapshot.PlayerHexCountsByAct.ToArray();
		int[] pendingEnemyHexCounts = pendingSnapshot.EnemyHexCountsByAct.ToArray();
		int[] pendingPlayerRuneRerollLimit = [ pendingSnapshot.PlayerRuneRerollLimit ];
		int[] pendingMonsterHexRerollLimit = [ pendingSnapshot.MonsterHexRerollLimit ];
		HashSet<string> pendingDisabledPlayerIds = pendingSnapshot.DisabledPlayerRuneIds.ToHashSet(StringComparer.Ordinal);
		HashSet<string> pendingDisabledMonsterHexIds = pendingSnapshot.DisabledMonsterHexIds.ToHashSet(StringComparer.Ordinal);
		HashSet<string> pendingDisabledForgeIds = pendingSnapshot.DisabledForgeIds.ToHashSet(StringComparer.Ordinal);
		int[][] pendingRuneWeightsByAct = pendingSnapshot.RuneRarityWeightsByAct
			.Select(ToWeightArray)
			.ToArray();
		int[] pendingGoldenRerollChancePercent = [ pendingSnapshot.GoldenRerollChancePercent ];
		int[] pendingForgeWeights = ToWeightArray(pendingSnapshot.ForgeRarityWeights);
		int[] pendingForgePrice = [ pendingSnapshot.RandomForgeShopPrice ];
		bool[] pendingShowHiddenRelicsToggle = [ HextechRelicVisibilityHooks.GetShowHiddenRelicsToggle() ];
		bool[] pendingShowUpdateNotice = [ HextechRelicVisibilityHooks.GetShowUpdateNotice() ];
		bool[] pendingCollapseEnemyHexes = [ HextechRelicVisibilityHooks.GetCollapseEnemyHexes() ];
		bool[] pendingRandomForgeDirectGrant = [ pendingSnapshot.RandomForgeDirectGrant ];
		bool[] pendingPreventConsecutiveSilverRunes = [ pendingSnapshot.PreventConsecutiveSilverRunes ];
		bool[] pendingModEnabled = [ pendingSnapshot.ModEnabled ];
		List<NumericValueBinding> numericBindings = [];
		List<BooleanValueBinding> booleanBindings = [];
		bool configReadOnly = IsEnemyHexCountConfigReadOnly();

		Label description = CreateLabel(string.Empty, compactLayout ? 13 : 15, new Color(0.82f, 0.86f, 0.92f, 0.92f));
		description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		description.HorizontalAlignment = HorizontalAlignment.Center;
		content.AddChild(description);
		void UpdateDescription(int pageIndex)
		{
			string text = pageIndex switch
			{
				0 => L(configReadOnly ? "HEXTECH_CONFIG_CLIENT_READONLY" : "HEXTECH_CONFIG_DESCRIPTION"),
				1 or 2 => L("HEXTECH_CONFIG_POOL_HINT"),
				3 => L("HEXTECH_CONFIG_MISC_HINT"),
				_ => string.Empty
			};
			SetLabelText(description, text);
			description.Visible = text.Length > 0;
		}

		List<RuneConfigEntry> playerEntries = BuildRuneEntries();
		List<RuneConfigEntry> enemyEntries = BuildEnemyHexEntries();
		List<RuneConfigEntry> forgeEntries = BuildForgeEntries();
		List<RuneIconBinding> playerIconBindings = [];
		List<RuneIconBinding> enemyIconBindings = [];
		List<RuneIconBinding> forgeIconBindings = [];
		List<RuneConfigLoadTarget> loadTargets = [];
		List<Action> badgeRefreshers = [];
		int selectedPageIndex = 0;
		Label summary = CreateLabel(string.Empty, compactLayout ? 15 : 16, new Color(0.92f, 0.88f, 0.7f, 0.95f));
		Action updateSummary = () =>
		{
			UpdateSummary(summary, selectedPageIndex, pendingDisabledPlayerIds, pendingDisabledMonsterHexIds, pendingDisabledForgeIds);
			foreach (Action refresh in badgeRefreshers)
			{
				refresh();
			}
		};

		// 分享区(杂项页)按钮的动作在 CreateBottomBar 里才能构建(依赖全部 pending 与 summary),延迟绑定。
		Action?[] shareActions = new Action?[3];
		Control countsPage = CreateSelectionPage(
			pendingPlayerHexCounts,
			pendingEnemyHexCounts,
			pendingPlayerRuneRerollLimit,
			pendingMonsterHexRerollLimit,
			pendingGoldenRerollChancePercent,
			numericBindings,
			compactLayout);
		Control runePoolPage = CreateRunePoolPage(playerEntries, pendingDisabledPlayerIds, enemyEntries, pendingDisabledMonsterHexIds, loadTargets, badgeRefreshers, compactLayout);
		Control forgePoolPage = CreateIconPoolPage(forgeEntries, pendingDisabledForgeIds, loadTargets, badgeRefreshers, L("HEXTECH_CONFIG_TAB_FORGES"), compactLayout);
		Control detailsPage = CreateDetailsPage(
			pendingRuneWeightsByAct,
			pendingForgeWeights,
			pendingPreventConsecutiveSilverRunes,
			pendingForgePrice,
			pendingShowHiddenRelicsToggle,
			pendingShowUpdateNotice,
			pendingCollapseEnemyHexes,
			pendingRandomForgeDirectGrant,
			pendingModEnabled,
			numericBindings,
			booleanBindings,
			shareActions,
			compactLayout);
		Control[] pageArray = [ countsPage, runePoolPage, forgePoolPage, detailsPage ];

		PanelContainer tabShell = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		tabShell.AddThemeStyleboxOverride("panel", CreateTabShellStyle());
		content.AddChild(tabShell);

		// Holder lets the sliding indicator overlay sit over the tab row without the
		// HBox laying it out as a sibling cell.
		Control tabHolder = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		tabShell.AddChild(tabHolder);

		HBoxContainer tabs = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		tabs.AddThemeConstantOverride("separation", 0);
		tabs.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		tabHolder.AddChild(tabs);

		// Added after the tabs so the gold underline draws on top of the active tab's
		// highlighted background instead of behind it.
		ColorRect tabIndicator = new()
		{
			Name = TabIndicatorName,
			Color = new Color(0.96f, 0.78f, 0.38f, 0.98f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		tabHolder.AddChild(tabIndicator);

		Vector2 tabButtonSize = GetTabButtonSize(compactLayout);
		tabHolder.CustomMinimumSize = new Vector2(tabButtonSize.X * 4f, tabButtonSize.Y);
		tabIndicator.Size = new Vector2(tabButtonSize.X, 3f);
		tabIndicator.Position = new Vector2(0f, tabButtonSize.Y - 3f);

		List<Button> tabButtons = [];
		Action<int>? updatePageActions = null;
		int previousPageIndex = -1;
		Action<int> selectPage = pageIndex =>
		{
			bool changed = pageIndex != previousPageIndex;
			previousPageIndex = pageIndex;
			selectedPageIndex = pageIndex;
			for (int i = 0; i < pageArray.Length; i++)
			{
				pageArray[i].Visible = i == pageIndex;
			}

			if (changed)
			{
				AnimatePageIn(pageArray[pageIndex]);
			}

			UpdateTabButtonStates(tabButtons, pageIndex, compactLayout);
			AnimateTabIndicator(tabButtons, pageIndex, changed);
			UpdateDescription(pageIndex);
			updatePageActions?.Invoke(pageIndex);
			updateSummary();
		};
		AddConfigTab(tabs, tabButtons, L("HEXTECH_CONFIG_TAB_COUNTS"), () => selectPage(0), compactLayout);
		AddConfigTab(tabs, tabButtons, L("HEXTECH_CONFIG_TAB_RUNE_POOLS"), () => selectPage(1), compactLayout);
		AddConfigTab(tabs, tabButtons, L("HEXTECH_CONFIG_TAB_FORGES"), () => selectPage(2), compactLayout);
		AddConfigTab(tabs, tabButtons, L("HEXTECH_CONFIG_TAB_DETAILS"), () => selectPage(3), compactLayout);

		ScrollContainer scroll = new()
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		VBoxContainer pages = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			// Pin a constant inner width to the widest page (the rune grid) so the panel
			// border does not resize when switching tabs.
			CustomMinimumSize = new Vector2(GetRuneGridMinWidth(compactLayout), 0f)
		};
		pages.AddThemeConstantOverride("separation", compactLayout ? 12 : 16);
		scroll.AddChild(pages);
		content.AddChild(scroll);

		content.AddChild(CreateBottomBar(
			overlay,
			playerEntries,
			enemyEntries,
			forgeEntries,
			pendingDisabledPlayerIds,
			pendingDisabledMonsterHexIds,
			pendingDisabledForgeIds,
			pendingPlayerHexCounts,
			pendingEnemyHexCounts,
			pendingPlayerRuneRerollLimit,
			pendingMonsterHexRerollLimit,
			pendingRuneWeightsByAct,
			pendingForgeWeights,
			pendingGoldenRerollChancePercent,
			pendingForgePrice,
			pendingShowHiddenRelicsToggle,
			pendingShowUpdateNotice,
			pendingCollapseEnemyHexes,
			pendingRandomForgeDirectGrant,
			pendingPreventConsecutiveSilverRunes,
			pendingModEnabled,
			numericBindings,
			booleanBindings,
			playerIconBindings,
			enemyIconBindings,
			forgeIconBindings,
			summary,
			updateSummary,
			() => selectedPageIndex,
			compactLayout,
			shareActions,
			out updatePageActions));

		foreach (Control page in pageArray)
		{
			page.Visible = false;
			pages.AddChild(page);
		}

		selectPage(0);
		updateSummary();
		state = new RuneConfigOverlayState(
			loadTargets,
			pendingDisabledPlayerIds,
			pendingDisabledMonsterHexIds,
			pendingDisabledForgeIds,
			playerIconBindings,
			enemyIconBindings,
			forgeIconBindings,
			tabButtons[0],
			tabButtons,
			updateSummary);
		return overlay;
	}

	private static int[] ToWeightArray(HextechRarityWeights weights)
	{
		return [ weights.Silver, weights.Gold, weights.Prismatic ];
	}

	private static int[] ToWeightArray(HextechForgeRarityWeights weights)
	{
		return [ weights.Silver, weights.Gold, weights.Prismatic ];
	}

	private static HextechRarityWeights ToRarityWeights(IReadOnlyList<int> weights)
	{
		return new HextechRarityWeights(
			weights.Count > 0 ? weights[0] : 0,
			weights.Count > 1 ? weights[1] : 0,
			weights.Count > 2 ? weights[2] : 0);
	}

	private static HextechRarityWeights[] ToRarityWeightsByAct(IEnumerable<IReadOnlyList<int>> weightsByAct)
	{
		return weightsByAct.Select(ToRarityWeights).ToArray();
	}

	private static HextechForgeRarityWeights ToForgeRarityWeights(IReadOnlyList<int> weights)
	{
		return new HextechForgeRarityWeights(
			weights.Count > 0 ? weights[0] : 0,
			weights.Count > 1 ? weights[1] : 0,
			weights.Count > 2 ? weights[2] : 0);
	}
}
