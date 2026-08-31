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
	private const string LocTable = "relic_collection";
	private const string ButtonName = "HextechRuneConfigButton";
	private const string OverlayName = "HextechRuneConfigOverlay";
	private const int MaxAttachAttempts = 30;
	private const int NativeDuplicateFlags = 14;
	private const int OverlayZIndex = 1000;
	private const int HoverTipZIndex = 2000;
	private const int RuneConfigColumns = 8;
	private const string BaseConfigSourceKey = "0:HextechRunes";
	private const string ExternalConfigSourcePrefix = "1:";
	private const string SponsorPackModId = "HextechRunesSponsorPack";
	private const float ConfigRuneHolderScale = 1.3f;
	private const float RuneConfigCellWidth = 108f;
	private const float RuneConfigCellHeight = 136f;
	private const float RuneConfigIconLayerHeight = 96f;
	private const float RuneConfigDragThreshold = 12f;
	private const float RuneConfigLongPressSeconds = 0.35f;
	private const float StepRepeatInitialDelaySeconds = 0.35f;
	private const float StepRepeatIntervalSeconds = 0.075f;
	private const float StepRepeatFastIntervalSeconds = 0.035f;
	private const int StepRepeatFastAfterTicks = 10;
	private const int RuneConfigIconsPerFrame = 12;
	private const float CompactConfigHeightThreshold = 820f;
	private const float OverlayOpenSeconds = 0.16f;
	private const float OverlayCloseSeconds = 0.12f;
	private const float OverlayOpenScale = 0.965f;
	private const float PageTransitionSeconds = 0.13f;
	private const float TabIndicatorSlideSeconds = 0.16f;
	private const float RuneStateFadeSeconds = 0.12f;
	private const float ToggleKnobSlideSeconds = 0.17f;
	private const string ConfigPanelName = "HextechRuneConfigPanel";
	private const string TabIndicatorName = "HextechRuneConfigTabIndicator";
	private static readonly FieldInfo? MainMenuButtonLocStringField = TryGetField(typeof(NMainMenuTextButton), "_locString");
	private static readonly FieldInfo? MainMenuLastHitButtonField = TryGetField(typeof(NMainMenu), "_lastHitButton");
	private static readonly MethodInfo? MainMenuButtonFocusedMethod = TryGetMethod(typeof(NMainMenu), "MainMenuButtonFocused", BindingFlags.Instance | BindingFlags.NonPublic, typeof(NMainMenuTextButton));
	private static readonly MethodInfo? MainMenuButtonUnfocusedMethod = TryGetMethod(typeof(NMainMenu), "MainMenuButtonUnfocused", BindingFlags.Instance | BindingFlags.NonPublic, typeof(NMainMenuTextButton));

	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(NMainMenu), nameof(NMainMenu._Ready), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechRuneConfigMenuHooks), nameof(MainMenuReadyPostfix)));
	}

	private static void MainMenuReadyPostfix(NMainMenu __instance)
	{
		TaskHelper.RunSafely(AttachButtonWhenReadyAsync(__instance));
	}

	private static async Task AttachButtonWhenReadyAsync(NMainMenu mainMenu)
	{
		for (int attempt = 1; attempt <= MaxAttachAttempts; attempt++)
		{
			if (!GodotObject.IsInstanceValid(mainMenu))
			{
				return;
			}

			try
			{
				if (TryAttachButton(mainMenu))
				{
					return;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][RuneConfig] Main menu button install failed: {ex.Message}", 2);
				return;
			}

			if (!await HextechGodotAsync.AwaitProcessFrameAsync(mainMenu))
			{
				return;
			}
		}

		Log.Warn($"[{ModInfo.Id}][RuneConfig] Main menu button skipped: root was not ready.", 2);
	}

	private static bool TryAttachButton(NMainMenu host)
	{
		if (host.FindChild(ButtonName, recursive: true, owned: false) is NMainMenuTextButton existingNative
			&& GodotObject.IsInstanceValid(existingNative))
		{
			return true;
		}

		if (TryAttachNativeMenuButton(host))
		{
			HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Main menu config button attached.");
			return true;
		}

		Log.Warn($"[{ModInfo.Id}][RuneConfig] Main menu config button skipped: native menu buttons were not available.", 2);
		return false;
	}

	private static bool TryAttachNativeMenuButton(NMainMenu mainMenu)
	{
		if (MainMenuButtonLocStringField == null)
		{
			return false;
		}

		if (mainMenu.GetNodeOrNull<Control>("MainMenuTextButtons") is not { } buttonHost
			|| mainMenu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton") is not { } settingsButton)
		{
			return false;
		}

		NMainMenuTextButton configButton = (NMainMenuTextButton)((Node)settingsButton).Duplicate(NativeDuplicateFlags);
		((Node)configButton).Name = ButtonName;
		((Node)configButton).UniqueNameInOwner = true;
		buttonHost.AddChild(configButton);
		buttonHost.MoveChild(configButton, Math.Min(settingsButton.GetIndex() + 1, buttonHost.GetChildCount() - 1));
		ConfigureNativeMenuLabel(configButton);
		ConfigureNativeMenuButton(configButton, settingsButton);
		ConfigureNativeMenuFocus(mainMenu, configButton);
		ConfigureNativeMenuNeighbors(buttonHost, configButton, settingsButton);
		ConnectNativeMenuButton(configButton);
		return true;
	}

	private static void ConfigureNativeMenuNeighbors(Control buttonHost, NMainMenuTextButton configButton, NMainMenuTextButton settingsButton)
	{
		Control configControl = configButton;
		Control settingsControl = settingsButton;
		configControl.FocusNeighborTop = settingsControl.GetPath();
		settingsControl.FocusNeighborBottom = configControl.GetPath();

		int nextIndex = configButton.GetIndex() + 1;
		if (nextIndex < buttonHost.GetChildCount() && buttonHost.GetChild(nextIndex) is Control nextControl)
		{
			configControl.FocusNeighborBottom = nextControl.GetPath();
			nextControl.FocusNeighborTop = configControl.GetPath();
		}
		else
		{
			configControl.FocusNeighborBottom = configControl.GetPath();
		}
	}

	private static void ConfigureNativeMenuLabel(NMainMenuTextButton configButton)
	{
		MainMenuButtonLocStringField?.SetValue(configButton, null);
		if (((Node)configButton).GetChildCount() > 0 && ((Node)configButton).GetChild(0) is Label label)
		{
			label.Text = L("HEXTECH_CONFIG_BUTTON");
			label.PivotOffset = label.Size * 0.5f;
		}

		((Control)configButton).TooltipText = L("HEXTECH_CONFIG_BUTTON_TOOLTIP");
	}

	private static void ConfigureNativeMenuButton(NMainMenuTextButton configButton, NMainMenuTextButton template)
	{
		Control control = configButton;
		control.MouseFilter = Control.MouseFilterEnum.Stop;
		control.FocusMode = Control.FocusModeEnum.All;
		control.MouseDefaultCursorShape = ((Control)template).MouseDefaultCursorShape;
		control.SizeFlagsHorizontal = template.SizeFlagsHorizontal;
		control.SizeFlagsVertical = template.SizeFlagsVertical;
		control.CustomMinimumSize = template.CustomMinimumSize;
		control.ZIndex = ((Control)template).ZIndex;
		control.ZAsRelative = ((Control)template).ZAsRelative;
	}

	private static void ConfigureNativeMenuFocus(NMainMenu mainMenu, NMainMenuTextButton configButton)
	{
		if (MainMenuButtonFocusedMethod != null)
		{
			((GodotObject)configButton).Connect(
				NClickableControl.SignalName.Focused,
				Callable.From<NMainMenuTextButton>(button =>
				{
					Callable.From(() => MainMenuButtonFocusedMethod.Invoke(mainMenu, [button])).CallDeferred();
				}));
		}

		if (MainMenuButtonUnfocusedMethod != null)
		{
			((GodotObject)configButton).Connect(
				NClickableControl.SignalName.Unfocused,
				Callable.From<NMainMenuTextButton>(button => MainMenuButtonUnfocusedMethod.Invoke(mainMenu, [button])));
		}
	}

	private static void ConnectNativeMenuButton(NMainMenuTextButton configButton)
	{
		((GodotObject)configButton).Connect(
			NClickableControl.SignalName.Released,
			Callable.From<NButton>(_ =>
			{
				if (FindAncestor<NMainMenu>(configButton) is { } mainMenu)
				{
					MainMenuLastHitButtonField?.SetValue(mainMenu, configButton);
				}

				OpenOverlay(configButton);
			}));
	}

	private static void OpenOverlay(Node source)
	{
		Node root = ResolveRoot(source);
		RemoveExistingOverlay(root);
		Control overlay = CreateOverlay(out RuneConfigOverlayState state);
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

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(overlay))
			{
				overlay.QueueFree();
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

	private static Control CreateSelectionPage(
		int[] pendingPlayerHexCounts,
		int[] pendingEnemyHexCounts,
		int[] pendingPlayerRuneRerollLimit,
		int[] pendingMonsterHexRerollLimit,
		int[] pendingGoldenRerollChancePercent,
		List<NumericValueBinding> numericBindings,
		bool compactLayout)
	{
		VBoxContainer page = CreatePageContainer(compactLayout);
		page.AddChild(CreateActCountSection(
			L("HEXTECH_PLAYER_COUNT_TITLE"),
			L("HEXTECH_PLAYER_COUNT_DESCRIPTION"),
			pendingPlayerHexCounts,
			HextechRuneConfiguration.ClampPlayerHexCount,
			numericBindings,
			compactLayout));
		page.AddChild(CreateActCountSection(
			L("HEXTECH_ENEMY_COUNT_TITLE"),
			L("HEXTECH_ENEMY_COUNT_DESCRIPTION"),
			pendingEnemyHexCounts,
			HextechRuneConfiguration.ClampEnemyHexCount,
			numericBindings,
			compactLayout));
		page.AddChild(CreateRerollLimitSection(
			pendingPlayerRuneRerollLimit,
			pendingMonsterHexRerollLimit,
			numericBindings,
			compactLayout));
		page.AddChild(CreateGoldenRerollChanceSection(
			pendingGoldenRerollChancePercent,
			numericBindings,
			compactLayout));
		return page;
	}

	private static Control CreateGoldenRerollChanceSection(
		int[] goldenRerollChancePercent,
		List<NumericValueBinding> numericBindings,
		bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(
			L("HEXTECH_GOLDEN_REROLL_CHANCE_LABEL"),
			null,
			compactLayout,
			out PanelContainer card);
		Label description = CreateLabel(
			L("HEXTECH_GOLDEN_REROLL_CHANCE_DESCRIPTION"),
			compactLayout ? 13 : 14,
			new Color(0.78f, 0.84f, 0.9f, 0.9f));
		description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		section.AddChild(description);

		HBoxContainer row = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddChild(CreateNumericStepper(
			L("HEXTECH_GOLDEN_REROLL_CHANCE_VALUE_LABEL"),
			() => goldenRerollChancePercent[0],
			value => goldenRerollChancePercent[0] = HextechRuneConfiguration.ClampGoldenRerollChancePercent(value),
			numericBindings,
			compactLayout,
			getDisplayText: () => $"{goldenRerollChancePercent[0]}%"));
		section.AddChild(row);
		return card;
	}

	private static VBoxContainer CreatePageContainer(bool compactLayout)
	{
		VBoxContainer page = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		page.AddThemeConstantOverride("separation", compactLayout ? 12 : 16);
		return page;
	}

	private static Control CreateActCountSection(
		string titleText,
		string descriptionText,
		int[] counts,
		Func<int, int> clamp,
		List<NumericValueBinding> numericBindings,
		bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(titleText, null, compactLayout, out PanelContainer card);
		Label description = CreateLabel(descriptionText, compactLayout ? 13 : 14, new Color(0.78f, 0.84f, 0.9f, 0.9f));
		description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		section.AddChild(description);

		HBoxContainer row = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 10 : 18);
		section.AddChild(row);
		row.AddChild(CreateNumericStepper(L("HEXTECH_ENEMY_COUNT_ACT1"), () => counts[0], value => counts[0] = clamp(value), numericBindings, compactLayout));
		row.AddChild(CreateNumericStepper(L("HEXTECH_ENEMY_COUNT_ACT2"), () => counts[1], value => counts[1] = clamp(value), numericBindings, compactLayout));
		row.AddChild(CreateNumericStepper(L("HEXTECH_ENEMY_COUNT_ACT3"), () => counts[2], value => counts[2] = clamp(value), numericBindings, compactLayout));
		return card;
	}

	private static Control CreateRerollLimitSection(
		int[] pendingPlayerRuneRerollLimit,
		int[] pendingMonsterHexRerollLimit,
		List<NumericValueBinding> numericBindings,
		bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(L("HEXTECH_REROLL_LIMIT_TITLE"), null, compactLayout, out PanelContainer card);
		Label description = CreateLabel(L("HEXTECH_REROLL_LIMIT_DESCRIPTION"), compactLayout ? 13 : 14, new Color(0.78f, 0.84f, 0.9f, 0.9f));
		description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		section.AddChild(description);

		HBoxContainer row = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 10 : 18);
		section.AddChild(row);
		row.AddChild(CreateRerollLimitStepper(
			L("HEXTECH_PLAYER_REROLL_LIMIT_LABEL"),
			() => pendingPlayerRuneRerollLimit[0],
			value => pendingPlayerRuneRerollLimit[0] = HextechRuneConfiguration.ClampRerollLimit(value),
			numericBindings,
			compactLayout));
		row.AddChild(CreateRerollLimitStepper(
			L("HEXTECH_MONSTER_REROLL_LIMIT_LABEL"),
			() => pendingMonsterHexRerollLimit[0],
			value => pendingMonsterHexRerollLimit[0] = HextechRuneConfiguration.ClampRerollLimit(value),
			numericBindings,
			compactLayout));
		return card;
	}

	private static Control CreateRunePoolPage(
		IReadOnlyList<RuneConfigEntry> playerEntries,
		HashSet<string> pendingDisabledPlayerIds,
		IReadOnlyList<RuneConfigEntry> enemyEntries,
		HashSet<string> pendingDisabledMonsterHexIds,
		List<RuneConfigLoadTarget> loadTargets,
		List<Action> badgeRefreshers,
		bool compactLayout)
	{
		VBoxContainer page = CreatePageContainer(compactLayout);
		page.AddChild(CreatePoolGroupHeader(L("HEXTECH_PLAYER_POOL_TITLE"), compactLayout));
		AddIconPoolEntries(page, playerEntries, pendingDisabledPlayerIds, loadTargets, badgeRefreshers, compactLayout);
		page.AddChild(CreatePoolGroupHeader(L("HEXTECH_ENEMY_POOL_TITLE"), compactLayout));
		AddIconPoolEntries(page, enemyEntries, pendingDisabledMonsterHexIds, loadTargets, badgeRefreshers, compactLayout);
		return page;
	}

	private static Control CreateIconPoolPage(
		IReadOnlyList<RuneConfigEntry> entries,
		HashSet<string> pendingDisabledIds,
		List<RuneConfigLoadTarget> loadTargets,
		List<Action> badgeRefreshers,
		string title,
		bool compactLayout)
	{
		VBoxContainer page = CreatePageContainer(compactLayout);
		page.AddChild(CreatePoolGroupHeader(title, compactLayout));
		AddIconPoolEntries(page, entries, pendingDisabledIds, loadTargets, badgeRefreshers, compactLayout);
		return page;
	}

	private static Label CreatePoolGroupHeader(string text, bool compactLayout)
	{
		Label label = CreateLabel(text, compactLayout ? 18 : 21, new Color(0.96f, 0.92f, 0.82f, 0.98f));
		label.CustomMinimumSize = new Vector2(0f, (compactLayout ? 18 : 21) + 6f);
		return label;
	}

	private static void AddIconPoolEntries(
		VBoxContainer page,
		IReadOnlyList<RuneConfigEntry> entries,
		HashSet<string> pendingDisabledIds,
		List<RuneConfigLoadTarget> loadTargets,
		List<Action> badgeRefreshers,
		bool compactLayout)
	{
		foreach (IGrouping<int, RuneConfigEntry> rarityGroup in entries.GroupBy(static entry => entry.RarityOrder))
		{
			List<RuneConfigEntry> groupEntries = rarityGroup.ToList();
			Color accent = GetRarityAccentColorByOrder(rarityGroup.Key);
			VBoxContainer card = CreateCardSection(string.Empty, accent, compactLayout, out PanelContainer cardNode);
			page.AddChild(cardNode);
			card.AddChild(CreateRarityGroupHeaderRow(
				groupEntries.First().RarityText,
				accent,
				groupEntries,
				pendingDisabledIds,
				badgeRefreshers,
				compactLayout));

			List<IGrouping<string, RuneConfigEntry>> sourceGroups = rarityGroup
				.GroupBy(static entry => entry.SourceKey)
				.ToList();
			foreach (IGrouping<string, RuneConfigEntry> sourceGroup in sourceGroups)
			{
				if (sourceGroups.Count > 1)
				{
					card.AddChild(CreateSourceHeader(sourceGroup.First().SourceText, compactLayout));
				}

				VBoxContainer grid = CreateRuneGrid(compactLayout);
				card.AddChild(grid);

				HBoxContainer? currentRow = null;
				int column = 0;
				foreach (RuneConfigEntry entry in sourceGroup)
				{
					if (column == 0)
					{
						currentRow = CreateRuneRow(compactLayout);
						grid.AddChild(currentRow);
					}

					CenterContainer slot = CreateRuneSlot();
					currentRow?.AddChild(slot);
					loadTargets.Add(new RuneConfigLoadTarget(entry, slot, pendingDisabledIds));

					column++;
					if (column == RuneConfigColumns)
					{
						column = 0;
					}
				}

				if (currentRow != null && column > 0)
				{
					for (; column < RuneConfigColumns; column++)
					{
						currentRow.AddChild(CreateRuneSlot());
					}
				}
			}
		}
	}

	private static Control CreateRarityGroupHeaderRow(
		string rarityText,
		Color accent,
		IReadOnlyList<RuneConfigEntry> groupEntries,
		HashSet<string> pendingDisabledIds,
		List<Action> badgeRefreshers,
		bool compactLayout)
	{
		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 8 : 12);

		Label title = CreateLabel(rarityText, compactLayout ? 16 : 18, accent);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		title.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(title);

		string[] groupIds = groupEntries.Select(static entry => entry.Id).ToArray();
		int total = groupIds.Length;
		Label badge = CreateLabel(string.Empty, compactLayout ? 13 : 14, accent);
		badge.HorizontalAlignment = HorizontalAlignment.Right;
		badge.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(badge);

		void Refresh()
		{
			int disabled = groupIds.Count(pendingDisabledIds.Contains);
			int enabled = Math.Max(0, total - disabled);
			SetLabelText(badge, $"{enabled}/{total}");
		}

		Refresh();
		badgeRefreshers.Add(Refresh);
		return row;
	}

	private static Control CreateDetailsPage(
		int[][] pendingRuneWeightsByAct,
		int[] pendingForgeWeights,
		bool[] pendingPreventConsecutiveSilverRunes,
		int[] pendingForgePrice,
		bool[] pendingShowHiddenRelicsToggle,
		bool[] pendingShowUpdateNotice,
		bool[] pendingCollapseEnemyHexes,
		bool[] pendingRandomForgeDirectGrant,
		bool[] pendingModEnabled,
		List<NumericValueBinding> numericBindings,
		List<BooleanValueBinding> booleanBindings,
		Action?[] shareActions,
		bool compactLayout)
	{
		VBoxContainer page = CreatePageContainer(compactLayout);
		page.AddChild(CreateMiscUiSection(pendingShowHiddenRelicsToggle, pendingShowUpdateNotice, pendingCollapseEnemyHexes, pendingRandomForgeDirectGrant, pendingModEnabled, booleanBindings, compactLayout));
		page.AddChild(CreateShareSection(shareActions, compactLayout));
		page.AddChild(CreatePriceSection(pendingForgePrice, numericBindings, compactLayout));
		page.AddChild(CreateWeightMatrixSection(
			pendingRuneWeightsByAct,
			pendingForgeWeights,
			pendingPreventConsecutiveSilverRunes,
			numericBindings,
			booleanBindings,
			compactLayout));
		return page;
	}

	private static Control CreateMiscUiSection(bool[] pendingShowHiddenRelicsToggle, bool[] pendingShowUpdateNotice, bool[] pendingCollapseEnemyHexes, bool[] pendingRandomForgeDirectGrant, bool[] pendingModEnabled, List<BooleanValueBinding> booleanBindings, bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(L("HEXTECH_MISC_UI_TITLE"), null, compactLayout, out PanelContainer card);
		section.AddChild(CreateBooleanOption(
			L("HEXTECH_MOD_ENABLED_TOGGLE_TITLE"),
			L("HEXTECH_MOD_ENABLED_TOGGLE_DESCRIPTION"),
			() => pendingModEnabled[0],
			value => pendingModEnabled[0] = value,
			booleanBindings,
			compactLayout));
		section.AddChild(CreateBooleanOption(
			L("HEXTECH_SHOW_UPDATE_NOTICE_TOGGLE_TITLE"),
			L("HEXTECH_SHOW_UPDATE_NOTICE_TOGGLE_DESCRIPTION"),
			() => pendingShowUpdateNotice[0],
			value => pendingShowUpdateNotice[0] = value,
			booleanBindings,
			compactLayout));
		section.AddChild(CreateBooleanOption(
			L("HEXTECH_COLLAPSE_ENEMY_HEXES_TOGGLE_TITLE"),
			L("HEXTECH_COLLAPSE_ENEMY_HEXES_TOGGLE_DESCRIPTION"),
			() => pendingCollapseEnemyHexes[0],
			value => pendingCollapseEnemyHexes[0] = value,
			booleanBindings,
			compactLayout));
		section.AddChild(CreateBooleanOption(
			L("HEXTECH_SHOW_HIDDEN_RELICS_TOGGLE_TITLE"),
			L("HEXTECH_SHOW_HIDDEN_RELICS_TOGGLE_DESCRIPTION"),
			() => pendingShowHiddenRelicsToggle[0],
			value => pendingShowHiddenRelicsToggle[0] = value,
			booleanBindings,
			compactLayout));
		section.AddChild(CreateBooleanOption(
			L("HEXTECH_RANDOM_FORGE_TOGGLE_TITLE"),
			L("HEXTECH_RANDOM_FORGE_TOGGLE_DESCRIPTION"),
			() => pendingRandomForgeDirectGrant[0],
			value => pendingRandomForgeDirectGrant[0] = value,
			booleanBindings,
			compactLayout));
		return card;
	}

	private static Control CreateBooleanOption(
		string titleText,
		string descriptionText,
		Func<bool> getValue,
		Action<bool> setValue,
		List<BooleanValueBinding> booleanBindings,
		bool compactLayout)
	{
		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 8 : 12);

		// 自绘开关(pill):关=深钢灰轨道+旋钮居左,开=金色轨道+旋钮滑到右,圆形旋钮。替代原生 CheckBox
		// 的默认主题图标,统一到本菜单的深蓝+金视觉语言。轨道颜色由按钮 pressed 态的 stylebox 自动切换,
		// 旋钮位置由 ApplyVisual 回调驱动(同时覆盖用户点击与"重置默认"的 SetPressedNoSignal 刷新)。
		float trackW = compactLayout ? 44f : 50f;
		float trackH = compactLayout ? 24f : 28f;
		float knobD = trackH - 6f;
		float knobOffX = 3f;
		float knobOnX = trackW - knobD - 3f;
		float knobY = (trackH - knobD) / 2f;

		Button toggle = new()
		{
			ToggleMode = true,
			Text = string.Empty,
			ButtonPressed = getValue(),
			CustomMinimumSize = new Vector2(trackW, trackH),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
		};
		StylePillTrack(toggle, trackH);

		Panel knob = new()
		{
			CustomMinimumSize = new Vector2(knobD, knobD),
			Size = new Vector2(knobD, knobD),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		knob.AddThemeStyleboxOverride("panel", CreatePillKnobStyle(knobD));
		toggle.AddChild(knob);

		// 旋钮以中心为锚做缩放回弹;位置另由 ApplyVisual 驱动。
		knob.PivotOffset = new Vector2(knobD / 2f, knobD / 2f);

		Tween? knobTween = null;
		void ApplyVisual(bool on, bool animate)
		{
			if (!GodotObject.IsInstanceValid(knob))
			{
				return;
			}

			Vector2 target = new(on ? knobOnX : knobOffX, knobY);
			if (knobTween != null && knobTween.IsValid())
			{
				knobTween.Kill();
			}

			knobTween = null;
			if (!animate || !knob.IsInsideTree())
			{
				// 首次构建(尚未进入场景树)或重置为默认时不做动画,直接落位。
				knob.Position = target;
				knob.Scale = Vector2.One;
				return;
			}

			// 开关切换:旋钮滑到目标位并带轻微过冲,同时做一次"按压回弹"缩放,手感更明确。
			knobTween = knob.CreateTween();
			knobTween.SetParallel(true);
			knobTween.TweenProperty(knob, "position", target, ToggleKnobSlideSeconds)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Back);
			knobTween.TweenProperty(knob, "scale", Vector2.One, ToggleKnobSlideSeconds)
				.From(new Vector2(0.78f, 0.78f))
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Back);
		}

		ApplyVisual(getValue(), animate: false);

		toggle.Toggled += value =>
		{
			setValue(value);
			ApplyVisual(value, animate: true);
		};
		booleanBindings.Add(new BooleanValueBinding(getValue, toggle, value => ApplyVisual(value, animate: true)));
		row.AddChild(toggle);

		VBoxContainer textColumn = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		textColumn.AddThemeConstantOverride("separation", compactLayout ? 2 : 4);
		Label title = CreateLabel(titleText, compactLayout ? 14 : 16, new Color(0.96f, 0.92f, 0.78f, 0.98f));
		title.HorizontalAlignment = HorizontalAlignment.Left;
		textColumn.AddChild(title);
		Label description = CreateLabel(descriptionText, compactLayout ? 12 : 13, new Color(0.78f, 0.84f, 0.9f, 0.88f));
		description.HorizontalAlignment = HorizontalAlignment.Left;
		description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		textColumn.AddChild(description);
		row.AddChild(textColumn);

		row.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
			{
				toggle.ButtonPressed = !toggle.ButtonPressed;
				row.GetViewport()?.SetInputAsHandled();
			}
		};
		return row;
	}

	private static Control CreatePriceSection(int[] price, List<NumericValueBinding> numericBindings, bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(L("HEXTECH_FORGE_PRICE_TITLE"), null, compactLayout, out PanelContainer card);
		HBoxContainer row = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddChild(CreateNumericStepper(
			L("HEXTECH_FORGE_PRICE_LABEL"),
			() => price[0],
			value => price[0] = HextechRuneConfiguration.ClampRandomForgeShopPrice(value),
			numericBindings,
			compactLayout,
			step: 10));
		section.AddChild(row);
		return card;
	}

	private static Control CreateWeightMatrixSection(
		int[][] runeWeightsByAct,
		int[] forgeWeights,
		bool[] preventConsecutiveSilverRunes,
		List<NumericValueBinding> numericBindings,
		List<BooleanValueBinding> booleanBindings,
		bool compactLayout)
	{
		VBoxContainer section = CreateCardSection(L("HEXTECH_RARITY_WEIGHTS_TITLE"), null, compactLayout, out PanelContainer card);
		Label description = CreateLabel(L("HEXTECH_RARITY_WEIGHTS_DESCRIPTION"), compactLayout ? 12 : 13, new Color(0.78f, 0.84f, 0.9f, 0.88f));
		description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		section.AddChild(description);

		GridContainer grid = new()
		{
			Columns = 4,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		grid.AddThemeConstantOverride("h_separation", compactLayout ? 8 : 16);
		grid.AddThemeConstantOverride("v_separation", compactLayout ? 8 : 14);
		section.AddChild(grid);

		// Header row: empty corner + three rarity column headers.
		grid.AddChild(new Control { CustomMinimumSize = new Vector2(compactLayout ? 76f : 110f, 0f) });
		grid.AddChild(CreateRarityColumnHeader(L("HEXTECH_RARITY_SILVER"), HextechRarityTier.Silver, compactLayout));
		grid.AddChild(CreateRarityColumnHeader(L("HEXTECH_RARITY_GOLD"), HextechRarityTier.Gold, compactLayout));
		grid.AddChild(CreateRarityColumnHeader(L("HEXTECH_RARITY_PRISMATIC"), HextechRarityTier.Prismatic, compactLayout));

		AddWeightMatrixRow(grid, L("HEXTECH_ENEMY_COUNT_ACT1"), runeWeightsByAct[0], numericBindings, compactLayout);
		AddWeightMatrixRow(grid, L("HEXTECH_ENEMY_COUNT_ACT2"), runeWeightsByAct[1], numericBindings, compactLayout);
		AddWeightMatrixRow(grid, L("HEXTECH_ENEMY_COUNT_ACT3"), runeWeightsByAct[2], numericBindings, compactLayout);
		AddWeightMatrixRow(grid, L("HEXTECH_RARITY_WEIGHTS_ROW_FORGE"), forgeWeights, numericBindings, compactLayout);
		section.AddChild(CreateBooleanOption(
			L("HEXTECH_PREVENT_CONSECUTIVE_SILVER_TOGGLE_TITLE"),
			L("HEXTECH_PREVENT_CONSECUTIVE_SILVER_TOGGLE_DESCRIPTION"),
			() => preventConsecutiveSilverRunes[0],
			value => preventConsecutiveSilverRunes[0] = value,
			booleanBindings,
			compactLayout));
		return card;
	}

	private static Label CreateRarityColumnHeader(string text, HextechRarityTier rarity, bool compactLayout)
	{
		Label label = CreateLabel(text, compactLayout ? 14 : 16, GetRarityAccentColor(rarity));
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return label;
	}

	private static void AddWeightMatrixRow(
		GridContainer grid,
		string rowLabel,
		int[] weights,
		List<NumericValueBinding> numericBindings,
		bool compactLayout)
	{
		Label label = CreateLabel(rowLabel, compactLayout ? 12 : 14, new Color(0.92f, 0.9f, 0.78f, 0.96f));
		label.HorizontalAlignment = HorizontalAlignment.Left;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		grid.AddChild(label);

		Action refreshRowPercents = () => { };
		for (int column = 0; column < 3; column++)
		{
			int index = column;
			grid.AddChild(CreateWeightMatrixCell(
				weights,
				index,
				GetRarityAccentColorByOrder(index),
				numericBindings,
				() => refreshRowPercents(),
				compactLayout,
				out Action refreshThisCell));
			Action previous = refreshRowPercents;
			refreshRowPercents = () =>
			{
				previous();
				refreshThisCell();
			};
		}
	}

	private static Control CreateWeightMatrixCell(
		int[] weights,
		int index,
		Color accent,
		List<NumericValueBinding> numericBindings,
		Action refreshRow,
		bool compactLayout,
		out Action refreshPercent)
	{
		VBoxContainer cell = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		cell.AddThemeConstantOverride("separation", compactLayout ? 1 : 3);

		HBoxContainer controls = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		controls.AddThemeConstantOverride("separation", compactLayout ? 5 : 7);
		cell.AddChild(controls);

		Label number = CreateLabel(weights[index].ToString(), compactLayout ? 16 : 18, new Color(0.98f, 0.98f, 0.94f, 1f));
		number.HorizontalAlignment = HorizontalAlignment.Center;
		number.VerticalAlignment = VerticalAlignment.Center;
		number.CustomMinimumSize = compactLayout ? new Vector2(36f, 30f) : new Vector2(46f, 34f);
		numericBindings.Add(new NumericValueBinding(() => weights[index].ToString(), number));

		string PercentText()
		{
			int total = weights[0] + weights[1] + weights[2];
			float percent = total > 0 ? weights[index] * 100f / total : 0f;
			return $"{percent:0.#}%";
		}

		Color percentColor = accent;
		percentColor.A = 0.78f;
		Label percent = CreateLabel(PercentText(), compactLayout ? 11 : 12, percentColor);
		percent.HorizontalAlignment = HorizontalAlignment.Center;
		numericBindings.Add(new NumericValueBinding(PercentText, percent));
		refreshPercent = () => SetLabelText(percent, PercentText());

		Button minus = CreateStepButton("-", compactLayout);
		Button plus = CreateStepButton("+", compactLayout);
		AttachRepeatingStep(minus, () =>
		{
			weights[index] = HextechRuneConfiguration.ClampRarityWeight(weights[index] - 1);
			SetLabelText(number, weights[index].ToString());
			refreshRow();
		});
		AttachRepeatingStep(plus, () =>
		{
			weights[index] = HextechRuneConfiguration.ClampRarityWeight(weights[index] + 1);
			SetLabelText(number, weights[index].ToString());
			refreshRow();
		});

		controls.AddChild(minus);
		controls.AddChild(number);
		controls.AddChild(plus);
		cell.AddChild(percent);
		return cell;
	}

	private static Control CreateNumericStepper(
		string labelText,
		Func<int> getValue,
		Action<int> setValue,
		List<NumericValueBinding> numericBindings,
		bool compactLayout,
		int step = 1,
		Func<string>? getDisplayText = null,
		Func<int, int, int>? stepValue = null)
	{
		VBoxContainer root = new()
		{
			CustomMinimumSize = compactLayout ? new Vector2(150f, 58f) : new Vector2(190f, 70f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		root.AddThemeConstantOverride("separation", compactLayout ? 3 : 5);

		Label label = CreateLabel(labelText, compactLayout ? 13 : 15, new Color(0.92f, 0.9f, 0.78f, 0.96f));
		label.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(label);

		HBoxContainer controls = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		controls.AddThemeConstantOverride("separation", compactLayout ? 6 : 8);
		root.AddChild(controls);

		string GetDisplay() => getDisplayText?.Invoke() ?? getValue().ToString();
		Label number = CreateLabel(GetDisplay(), compactLayout ? 17 : 18, new Color(0.98f, 0.98f, 0.94f, 1f));
		number.HorizontalAlignment = HorizontalAlignment.Center;
		number.VerticalAlignment = VerticalAlignment.Center;
		number.CustomMinimumSize = compactLayout ? new Vector2(44f, 32f) : new Vector2(54f, 34f);
		numericBindings.Add(new NumericValueBinding(GetDisplay, number));

		Button minus = CreateStepButton("-", compactLayout);
		Button plus = CreateStepButton("+", compactLayout);
		AttachRepeatingStep(minus, () =>
		{
			setValue(stepValue?.Invoke(getValue(), -step) ?? getValue() - step);
			SetLabelText(number, GetDisplay());
		});
		AttachRepeatingStep(plus, () =>
		{
			setValue(stepValue?.Invoke(getValue(), step) ?? getValue() + step);
			SetLabelText(number, GetDisplay());
		});

		controls.AddChild(minus);
		controls.AddChild(number);
		controls.AddChild(plus);
		return root;
	}

	private static Control CreateRerollLimitStepper(
		string labelText,
		Func<int> getValue,
		Action<int> setValue,
		List<NumericValueBinding> numericBindings,
		bool compactLayout)
	{
		return CreateNumericStepper(
			labelText,
			getValue,
			setValue,
			numericBindings,
			compactLayout,
			getDisplayText: () => FormatRerollLimit(getValue()),
			stepValue: static (current, delta) => HextechRuneConfiguration.StepRerollLimit(current, delta));
	}

	private static string FormatRerollLimit(int value)
	{
		return HextechRuneConfiguration.ClampRerollLimit(value) == HextechRuneConfiguration.InfiniteRerollLimit
			? L("HEXTECH_REROLL_LIMIT_INFINITE")
			: HextechRuneConfiguration.ClampRerollLimit(value).ToString();
	}

	private static void AddConfigTab(HBoxContainer tabs, List<Button> tabButtons, string text, Action action, bool compactLayout)
	{
		Button button = CreateTabButton(text, action, compactLayout);
		tabButtons.Add(button);
		tabs.AddChild(button);
	}

	private static Button CreateTabButton(string text, Action action, bool compactLayout)
	{
		Button button = new()
		{
			Text = string.Empty,
			CustomMinimumSize = GetTabButtonSize(compactLayout),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			FocusMode = Control.FocusModeEnum.All
		};
		AddCrispButtonText(button, text, compactLayout ? 14 : 16, new Color(0.96f, 0.94f, 0.88f, 1f));
		button.Pressed += action;
		return button;
	}

	private static Vector2 GetTabButtonSize(bool compactLayout)
	{
		return compactLayout ? new Vector2(108f, 36f) : new Vector2(154f, 42f);
	}

	private static StyleBoxFlat CreateTabShellStyle()
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.07f, 0.085f, 0.12f, 0.92f),
			BorderColor = new Color(0.46f, 0.55f, 0.68f, 0.34f)
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(12);
		style.ContentMarginLeft = 4;
		style.ContentMarginRight = 4;
		style.ContentMarginTop = 4;
		style.ContentMarginBottom = 4;
		return style;
	}

	private static void UpdateTabButtonStates(IReadOnlyList<Button> tabButtons, int selectedIndex, bool compactLayout)
	{
		for (int i = 0; i < tabButtons.Count; i++)
		{
			ApplyTabButtonState(tabButtons[i], i == selectedIndex, compactLayout);
		}
	}

	private static void ApplyTabButtonState(Button button, bool active, bool compactLayout)
	{
		button.AddThemeStyleboxOverride("normal", CreateTabSegmentStyle(active, false));
		button.AddThemeStyleboxOverride("hover", CreateTabSegmentStyle(active, true));
		button.AddThemeStyleboxOverride("pressed", CreateTabSegmentStyle(active, true));
		button.AddThemeStyleboxOverride("focus", CreateTabSegmentStyle(active, true));
		if (button.GetChildCount() > 0 && button.GetChild(0) is Label label)
		{
			label.Modulate = active
				? new Color(1f, 0.86f, 0.5f, 1f)
				: new Color(0.78f, 0.82f, 0.88f, 0.86f);
		}
	}

	private static StyleBoxFlat CreateTabSegmentStyle(bool active, bool hovered)
	{
		Color background = active
			? new Color(0.17f, 0.2f, 0.27f, 0.98f)
			: hovered ? new Color(0.13f, 0.16f, 0.22f, 0.82f) : new Color(0f, 0f, 0f, 0f);
		StyleBoxFlat style = new()
		{
			BgColor = background
		};
		style.SetCornerRadiusAll(9);
		// The active underline is drawn by the sliding indicator overlay, not per-button.
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 5;
		style.ContentMarginBottom = 5;
		return style;
	}

	private static void AnimatePageIn(Control page)
	{
		if (!GodotObject.IsInstanceValid(page))
		{
			return;
		}

		// Pages live in a VBoxContainer that owns their position, so animate opacity only —
		// a positional tween would fight the container's layout each frame.
		page.Modulate = new Color(1f, 1f, 1f, 0f);
		Tween tween = page.CreateTween();
		tween.TweenProperty(page, "modulate:a", 1f, PageTransitionSeconds).SetEase(Tween.EaseType.Out);
	}

	private static void AnimateTabIndicator(IReadOnlyList<Button> tabButtons, int activeIndex, bool animated)
	{
		if (activeIndex < 0 || activeIndex >= tabButtons.Count)
		{
			return;
		}

		Button active = tabButtons[activeIndex];
		if (!GodotObject.IsInstanceValid(active)
			|| active.GetParent()?.GetParent() is not Control holder
			|| holder.GetNodeOrNull<ColorRect>(TabIndicatorName) is not { } indicator)
		{
			return;
		}

		// Resolve the active button's rect relative to the holder once layout settles.
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(active) || !GodotObject.IsInstanceValid(indicator))
			{
				return;
			}

			float targetX = active.Position.X;
			float targetWidth = active.Size.X > 0f ? active.Size.X : indicator.Size.X;
			float targetY = active.Position.Y + active.Size.Y - 3f;
			Vector2 targetPos = new(targetX, targetY);
			Vector2 targetSize = new(targetWidth, 3f);
			if (!animated || indicator.Size.X <= 0f)
			{
				indicator.Position = targetPos;
				indicator.Size = targetSize;
				return;
			}

			Tween tween = indicator.CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(indicator, "position", targetPos, TabIndicatorSlideSeconds)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			tween.TweenProperty(indicator, "size", targetSize, TabIndicatorSlideSeconds)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
		}).CallDeferred();
	}

	private static Control CreateBottomBar(
		Control overlay,
		IReadOnlyList<RuneConfigEntry> playerEntries,
		IReadOnlyList<RuneConfigEntry> enemyEntries,
		IReadOnlyList<RuneConfigEntry> forgeEntries,
		HashSet<string> pendingDisabledPlayerIds,
		HashSet<string> pendingDisabledMonsterHexIds,
		HashSet<string> pendingDisabledForgeIds,
		int[] pendingPlayerHexCounts,
		int[] pendingEnemyHexCounts,
		int[] pendingPlayerRuneRerollLimit,
		int[] pendingMonsterHexRerollLimit,
		int[][] pendingRuneWeightsByAct,
		int[] pendingForgeWeights,
		int[] pendingGoldenRerollChancePercent,
		int[] pendingForgePrice,
		bool[] pendingShowHiddenRelicsToggle,
		bool[] pendingShowUpdateNotice,
		bool[] pendingCollapseEnemyHexes,
		bool[] pendingRandomForgeDirectGrant,
		bool[] pendingPreventConsecutiveSilverRunes,
		bool[] pendingModEnabled,
		IReadOnlyList<NumericValueBinding> numericBindings,
		IReadOnlyList<BooleanValueBinding> booleanBindings,
		IReadOnlyList<RuneIconBinding> playerIconBindings,
		IReadOnlyList<RuneIconBinding> enemyIconBindings,
		IReadOnlyList<RuneIconBinding> forgeIconBindings,
		Label summary,
		Action updateSummary,
		Func<int> getPageIndex,
		bool compactLayout,
		Action?[] shareActions,
		out Action<int> updatePageActions)
	{
		VBoxContainer bar = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		bar.AddThemeConstantOverride("separation", compactLayout ? 6 : 9);

		ColorRect hairline = new()
		{
			Color = new Color(0.86f, 0.74f, 0.42f, 0.28f),
			CustomMinimumSize = new Vector2(0f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bar.AddChild(hairline);

		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 7 : 12);

		Button enableAll = CreateActionButton(L("HEXTECH_CONFIG_ENABLE_ALL"), () =>
		{
			switch (getPageIndex())
			{
				case 1:
					pendingDisabledPlayerIds.Clear();
					pendingDisabledMonsterHexIds.Clear();
					UpdateAllRuneIcons(playerIconBindings, pendingDisabledPlayerIds);
					UpdateAllRuneIcons(enemyIconBindings, pendingDisabledMonsterHexIds);
					break;
				case 2:
					pendingDisabledForgeIds.Clear();
					UpdateAllRuneIcons(forgeIconBindings, pendingDisabledForgeIds);
					break;
			}

			updateSummary();
		}, compactLayout);
		Button disableAll = CreateActionButton(L("HEXTECH_CONFIG_DISABLE_ALL"), () =>
		{
			switch (getPageIndex())
			{
				case 1:
					ReplaceDisabledIds(pendingDisabledPlayerIds, playerEntries);
					ReplaceDisabledIds(pendingDisabledMonsterHexIds, enemyEntries);
					UpdateAllRuneIcons(playerIconBindings, pendingDisabledPlayerIds);
					UpdateAllRuneIcons(enemyIconBindings, pendingDisabledMonsterHexIds);
					break;
				case 2:
					ReplaceDisabledIds(pendingDisabledForgeIds, forgeEntries);
					UpdateAllRuneIcons(forgeIconBindings, pendingDisabledForgeIds);
					break;
			}

			updateSummary();
		}, compactLayout);
		Button reset = CreateActionButton(L("HEXTECH_CONFIG_RESET"), () =>
		{
			HextechRunConfigurationSnapshot defaults = HextechRuneConfiguration.GetDefaultSnapshot();
			switch (getPageIndex())
			{
				case 0:
					CopyArray(defaults.PlayerHexCountsByAct, pendingPlayerHexCounts);
					CopyArray(defaults.EnemyHexCountsByAct, pendingEnemyHexCounts);
					pendingPlayerRuneRerollLimit[0] = defaults.PlayerRuneRerollLimit;
					pendingMonsterHexRerollLimit[0] = defaults.MonsterHexRerollLimit;
					UpdateNumericLabels(numericBindings);
					break;
				case 1:
					pendingDisabledPlayerIds.Clear();
					pendingDisabledPlayerIds.UnionWith(defaults.DisabledPlayerRuneIds);
					pendingDisabledMonsterHexIds.Clear();
					pendingDisabledMonsterHexIds.UnionWith(defaults.DisabledMonsterHexIds);
					UpdateAllRuneIcons(playerIconBindings, pendingDisabledPlayerIds);
					UpdateAllRuneIcons(enemyIconBindings, pendingDisabledMonsterHexIds);
					break;
				case 2:
					pendingDisabledForgeIds.Clear();
					pendingDisabledForgeIds.UnionWith(defaults.DisabledForgeIds);
					UpdateAllRuneIcons(forgeIconBindings, pendingDisabledForgeIds);
					break;
				case 3:
					for (int actIndex = 0; actIndex < pendingRuneWeightsByAct.Length; actIndex++)
					{
						CopyArray(ToWeightArray(defaults.RuneRarityWeightsByAct[actIndex]), pendingRuneWeightsByAct[actIndex]);
					}
					CopyArray(ToWeightArray(defaults.ForgeRarityWeights), pendingForgeWeights);
					pendingGoldenRerollChancePercent[0] = defaults.GoldenRerollChancePercent;
					pendingForgePrice[0] = defaults.RandomForgeShopPrice;
					pendingShowHiddenRelicsToggle[0] = HextechRelicVisibilityHooks.GetDefaultShowHiddenRelicsToggle();
					pendingShowUpdateNotice[0] = HextechRelicVisibilityHooks.GetDefaultShowUpdateNotice();
					pendingCollapseEnemyHexes[0] = HextechRelicVisibilityHooks.GetDefaultCollapseEnemyHexes();
					pendingRandomForgeDirectGrant[0] = defaults.RandomForgeDirectGrant;
					pendingPreventConsecutiveSilverRunes[0] = defaults.PreventConsecutiveSilverRunes;
					pendingModEnabled[0] = defaults.ModEnabled;
					UpdateNumericLabels(numericBindings);
					UpdateBooleanToggles(booleanBindings);
					break;
			}

			updateSummary();
		}, compactLayout);

		Button save = CreateActionButton(L("HEXTECH_CONFIG_SAVE_CLOSE"), () =>
		{
			HextechRuneConfiguration.SaveSnapshot(new HextechRunConfigurationSnapshot(
				pendingPlayerHexCounts,
				pendingEnemyHexCounts,
				pendingPlayerRuneRerollLimit[0],
				pendingMonsterHexRerollLimit[0],
				pendingDisabledPlayerIds,
				pendingDisabledMonsterHexIds,
				pendingDisabledForgeIds,
				ToRarityWeightsByAct(pendingRuneWeightsByAct),
				pendingPreventConsecutiveSilverRunes[0],
				pendingGoldenRerollChancePercent[0],
				ToForgeRarityWeights(pendingForgeWeights),
				pendingForgePrice[0],
				pendingRandomForgeDirectGrant[0],
				pendingModEnabled[0]));
			HextechRelicVisibilityHooks.SetShowHiddenRelicsToggle(pendingShowHiddenRelicsToggle[0]);
			HextechRelicVisibilityHooks.SetShowUpdateNotice(pendingShowUpdateNotice[0]);
			HextechRelicVisibilityHooks.SetCollapseEnemyHexes(pendingCollapseEnemyHexes[0]);
			HextechUpdateChecker.ApplyNoticeVisibility(overlay);
			HextechCollectionHooks.RefreshOpenRelicCollections();
			string runeWeights = string.Join("/", pendingRuneWeightsByAct.Select(static weights => string.Join(",", weights)));
			HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Saved run config: playerDisabled={pendingDisabledPlayerIds.Count} enemyDisabled={pendingDisabledMonsterHexIds.Count} forgeDisabled={pendingDisabledForgeIds.Count} playerCounts={string.Join(",", pendingPlayerHexCounts)} enemyCounts={string.Join(",", pendingEnemyHexCounts)} playerRerolls={pendingPlayerRuneRerollLimit[0]} monsterRerolls={pendingMonsterHexRerollLimit[0]} runeWeightsByAct={runeWeights} preventConsecutiveSilver={pendingPreventConsecutiveSilverRunes[0]} goldenRerollChance={pendingGoldenRerollChancePercent[0]}% forgePrice={pendingForgePrice[0]} showHiddenUiToggle={pendingShowHiddenRelicsToggle[0]} showUpdateNotice={pendingShowUpdateNotice[0]} randomForgeDirect={pendingRandomForgeDirectGrant[0]} modEnabled={pendingModEnabled[0]}");
			CloseOverlayAnimated(overlay);
		}, compactLayout);
		Button cancel = CreateActionButton(L("HEXTECH_CONFIG_CANCEL"), () => CloseWithoutSaving(overlay), compactLayout);

		// 配置分享码：导出=把当前编辑中的配置(pending 态)编码进剪贴板;导入=从剪贴板解析并填充
		// pending 态(界面即预览,可继续修改,「取消」可放弃)——真正落盘仍走「保存并关闭」。
		// 按钮本体放在「杂项」页的分享区(CreateShareSection),这里只填充延迟绑定的动作。
		Func<string> buildPendingCode = () => HextechConfigShareCodec.Export(new HextechRunConfigurationSnapshot(
			pendingPlayerHexCounts,
			pendingEnemyHexCounts,
			pendingPlayerRuneRerollLimit[0],
			pendingMonsterHexRerollLimit[0],
			pendingDisabledPlayerIds,
			pendingDisabledMonsterHexIds,
			pendingDisabledForgeIds,
			ToRarityWeightsByAct(pendingRuneWeightsByAct),
			pendingPreventConsecutiveSilverRunes[0],
			pendingGoldenRerollChancePercent[0],
			ToForgeRarityWeights(pendingForgeWeights),
			pendingForgePrice[0],
			pendingRandomForgeDirectGrant[0],
			pendingModEnabled[0]));
		shareActions[0] = () =>
		{
			string code = buildPendingCode();
			DisplayServer.ClipboardSet(code);
			updateSummary();
			summary.Text = L("HEXTECH_CONFIG_EXPORT_DONE");
		};
		// 把分享码/社区配置解析结果填充进 pending 编辑态并刷新全部控件(界面即预览,「取消」可放弃)。
		Action<HextechConfigShareCodec.ImportPreview> applyPreview = preview =>
		{
			HextechRunConfigurationSnapshot imported = preview.Snapshot;
			CopyArray(imported.PlayerHexCountsByAct, pendingPlayerHexCounts);
			CopyArray(imported.EnemyHexCountsByAct, pendingEnemyHexCounts);
			pendingPlayerRuneRerollLimit[0] = imported.PlayerRuneRerollLimit;
			pendingMonsterHexRerollLimit[0] = imported.MonsterHexRerollLimit;
			pendingDisabledPlayerIds.Clear();
			pendingDisabledPlayerIds.UnionWith(imported.DisabledPlayerRuneIds);
			pendingDisabledMonsterHexIds.Clear();
			pendingDisabledMonsterHexIds.UnionWith(imported.DisabledMonsterHexIds);
			pendingDisabledForgeIds.Clear();
			pendingDisabledForgeIds.UnionWith(imported.DisabledForgeIds);
			for (int actIndex = 0; actIndex < pendingRuneWeightsByAct.Length; actIndex++)
			{
				CopyArray(ToWeightArray(imported.RuneRarityWeightsByAct[actIndex]), pendingRuneWeightsByAct[actIndex]);
			}
			CopyArray(ToWeightArray(imported.ForgeRarityWeights), pendingForgeWeights);
			pendingForgePrice[0] = imported.RandomForgeShopPrice;
			pendingRandomForgeDirectGrant[0] = imported.RandomForgeDirectGrant;
			pendingPreventConsecutiveSilverRunes[0] = imported.PreventConsecutiveSilverRunes;
			pendingGoldenRerollChancePercent[0] = imported.GoldenRerollChancePercent;
			// ModEnabled 与 UI 偏好(折叠/隐藏遗物开关等)不随导入改变。
			UpdateNumericLabels(numericBindings);
			UpdateBooleanToggles(booleanBindings);
			UpdateAllRuneIcons(playerIconBindings, pendingDisabledPlayerIds);
			UpdateAllRuneIcons(enemyIconBindings, pendingDisabledMonsterHexIds);
			UpdateAllRuneIcons(forgeIconBindings, pendingDisabledForgeIds);
			updateSummary();
			summary.Text = string.Format(L("HEXTECH_CONFIG_IMPORT_DONE"), preview.IgnoredUnknownCount);
		};

		shareActions[1] = () =>
		{
			HextechConfigShareCodec.ImportPreview? preview = HextechConfigShareCodec.TryParse(DisplayServer.ClipboardGet());
			if (preview == null)
			{
				updateSummary();
				summary.Text = L("HEXTECH_CONFIG_IMPORT_INVALID");
				return;
			}

			applyPreview(preview);
		};
		shareActions[2] = () => OpenCommunityConfigsPanel(overlay, applyPreview, buildPendingCode, compactLayout);

		// Summary lives on its own centered, wrapping line so its variable width never
		// drives the panel width. It always reserves a line of height to keep the panel
		// size stable across pages.
		summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		summary.HorizontalAlignment = HorizontalAlignment.Center;
		summary.VerticalAlignment = VerticalAlignment.Center;
		summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		summary.CustomMinimumSize = new Vector2(0f, compactLayout ? 18f : 20f);
		bar.AddChild(summary);

		Control spacer = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bar.AddChild(row);

		// Stable order: Reset / Enable All / Disable All pinned left; Save / Cancel pinned right.
		row.AddChild(reset);
		row.AddChild(enableAll);
		row.AddChild(disableAll);
		row.AddChild(spacer);
		row.AddChild(save);
		row.AddChild(cancel);

		updatePageActions = pageIndex =>
		{
			bool showPoolBulkActions = pageIndex is 1 or 2;
			enableAll.Visible = showPoolBulkActions;
			disableAll.Visible = showPoolBulkActions;
		};
		return bar;
	}

	private static void ReplaceDisabledIds(HashSet<string> target, IEnumerable<RuneConfigEntry> entries)
	{
		target.Clear();
		foreach (RuneConfigEntry entry in entries)
		{
			target.Add(entry.Id);
		}
	}

	private static void CopyArray(IReadOnlyList<int> source, int[] target)
	{
		for (int i = 0; i < Math.Min(source.Count, target.Length); i++)
		{
			target[i] = source[i];
		}
	}

	private static Vector2 GetResponsivePanelSize()
	{
		Vector2I windowSize = DisplayServer.WindowGetSize();
		float windowWidth = windowSize.X > 0 ? windowSize.X : 1280f;
		float windowHeight = windowSize.Y > 0 ? windowSize.Y : 720f;
		bool compactLayout = windowHeight < CompactConfigHeightThreshold;
		// Panel must always be wide enough to hold the rune grid plus its own margins so the
		// border width stays constant across pages. Keep an upper bound for very wide screens.
		float panelMargins = (compactLayout ? 20f : 28f) * 2f;
		float minWidth = GetRuneGridMinWidth(compactLayout) + panelMargins;
		float maxWidth = Math.Max(minWidth, 1080f);
		float width = windowWidth < minWidth
			? Math.Max(320f, windowWidth * 0.98f)
			: Mathf.Clamp(windowWidth * 0.9f, minWidth, maxWidth);
		float height = windowHeight < CompactConfigHeightThreshold
			? Math.Max(440f, windowHeight * 0.98f)
			: Mathf.Clamp(windowHeight * 0.92f, 660f, 840f);
		return new Vector2(width, height);
	}

	private static float GetRuneGridMinWidth(bool compactLayout)
	{
		float rowSeparation = compactLayout ? 6f : 8f;
		float cardMargins = (compactLayout ? 14f : 20f) * 2f;
		return RuneConfigColumns * RuneConfigCellWidth
			+ (RuneConfigColumns - 1) * rowSeparation
			+ cardMargins;
	}

	private static bool IsCompactConfigLayout()
	{
		Vector2I windowSize = DisplayServer.WindowGetSize();
		float windowHeight = windowSize.Y > 0 ? windowSize.Y : 720f;
		return windowHeight < CompactConfigHeightThreshold;
	}

	private static Button CreateStepButton(string text, bool compactLayout)
	{
		Button button = new()
		{
			Text = string.Empty,
			FocusMode = Control.FocusModeEnum.All,
			CustomMinimumSize = compactLayout ? new Vector2(34f, 32f) : new Vector2(38f, 34f),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.1f, 0.12f, 0.17f, 0.9f), new Color(0.46f, 0.55f, 0.68f, 0.78f)));
		button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.13f, 0.16f, 0.22f, 0.95f), new Color(0.88f, 0.72f, 0.36f, 0.92f)));
		button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.07f, 0.09f, 0.13f, 0.98f), new Color(0.88f, 0.62f, 0.28f, 0.92f)));
		AddCrispButtonText(button, text, compactLayout ? 17 : 18, new Color(0.96f, 0.94f, 0.88f, 1f));
		return button;
	}

	private static void AttachRepeatingStep(Button button, Action action)
	{
		int pressToken = 0;

		button.ButtonDown += () =>
		{
			if (button.Disabled)
			{
				return;
			}

			pressToken++;
			int currentToken = pressToken;
			action();
			TaskHelper.RunSafely(RepeatStepAsync(button, currentToken, () => pressToken == currentToken, action));
		};
		button.ButtonUp += () => pressToken++;
		button.TreeExiting += () => pressToken++;
	}

	private static async Task RepeatStepAsync(Button button, int token, Func<bool> tokenIsCurrent, Action action)
	{
		if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
		{
			return;
		}

		SceneTree tree = button.GetTree();
		if (tree == null)
		{
			return;
		}

		await button.ToSignal(tree.CreateTimer(StepRepeatInitialDelaySeconds), "timeout");
		int repeatCount = 0;
		while (GodotObject.IsInstanceValid(button)
			&& button.IsInsideTree()
			&& button.ButtonPressed
			&& !button.Disabled
			&& tokenIsCurrent())
		{
			action();
			repeatCount++;
			float interval = repeatCount >= StepRepeatFastAfterTicks
				? StepRepeatFastIntervalSeconds
				: StepRepeatIntervalSeconds;
			await button.ToSignal(tree.CreateTimer(interval), "timeout");
		}
	}

	private static bool IsEnemyHexCountConfigReadOnly()
	{
		return HextechPlayerContextHelper.IsClientRun();
	}

	private static void CloseWithoutSaving(Control overlay)
	{
		if (!GodotObject.IsInstanceValid(overlay))
		{
			return;
		}

		overlay.GetViewport()?.SetInputAsHandled();
		CloseOverlayAnimated(overlay);
	}

	private static Label CreateSectionHeader(string text, int fontSize = 20)
	{
		Label label = CreateLabel(text, fontSize, new Color(0.96f, 0.84f, 0.48f, 0.98f));
		label.CustomMinimumSize = new Vector2(0f, fontSize + 6f);
		return label;
	}

	private static Label CreateSourceHeader(string text, bool compactLayout)
	{
		Label label = CreateLabel(text, compactLayout ? 14 : 15, new Color(0.68f, 0.82f, 0.98f, 0.92f));
		label.CustomMinimumSize = new Vector2(0f, compactLayout ? 18f : 22f);
		return label;
	}

	private static VBoxContainer CreateRuneGrid(bool compactLayout)
	{
		VBoxContainer grid = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		grid.AddThemeConstantOverride("separation", compactLayout ? 5 : 7);
		return grid;
	}

	private static HBoxContainer CreateRuneRow(bool compactLayout)
	{
		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 6 : 8);
		return row;
	}

	private static CenterContainer CreateRuneSlot()
	{
		return new CenterContainer()
		{
			CustomMinimumSize = new Vector2(RuneConfigCellWidth, RuneConfigCellHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
	}

	private static RuneIconBinding CreateRuneIcon(RuneConfigEntry entry, HashSet<string> pendingDisabledIds, Action updateSummary)
	{
		VBoxContainer root = new()
		{
			Name = "RuneConfigIcon_" + entry.Id,
			CustomMinimumSize = new Vector2(RuneConfigCellWidth, RuneConfigCellHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		root.AddThemeConstantOverride("separation", 2);

		Control iconLayer = new()
		{
			CustomMinimumSize = new Vector2(RuneConfigCellWidth, RuneConfigIconLayerHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		CenterContainer iconCenter = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		ApplyConfigIconScale(iconCenter);
		NRelicBasicHolder holder = NRelicBasicHolder.Create(entry.Relic)
			?? throw new InvalidOperationException($"Failed to create config relic holder for {entry.Id}.");
		holder.MouseFilter = Control.MouseFilterEnum.Ignore;
		iconCenter.AddChild(holder);
		iconLayer.AddChild(iconCenter);
		root.AddChild(iconLayer);

		Label title = CreateRuneNameLabel(entry.Title);
		root.AddChild(title);

		RuneIconBinding binding = new(entry.Id, root, holder, title);
		ApplyRuneIconState(binding, !pendingDisabledIds.Contains(entry.Id));
		AttachRuneToggleInput(root, entry, binding, pendingDisabledIds, updateSummary);
		AttachRelicHoverTips(root, entry.Relic, GetEnemyHexKind(entry));
		root.FocusEntered += () => root.SelfModulate = new Color(1.12f, 1.12f, 1.12f, 1f);
		root.FocusExited += () => root.SelfModulate = Colors.White;
		return binding;
	}

	private static void ApplyConfigIconScale(Control control)
	{
		control.Scale = Vector2.One * ConfigRuneHolderScale;
		control.PivotOffset = new Vector2(RuneConfigCellWidth, RuneConfigIconLayerHeight) * 0.5f;
		control.Resized += () =>
		{
			if (GodotObject.IsInstanceValid(control))
			{
				control.PivotOffset = control.Size * 0.5f;
			}
		};
	}

	private static async Task PopulateRuneIconsAsync(Control overlay, RuneConfigOverlayState state)
	{
		if (!await HextechGodotAsync.AwaitProcessFrameAsync(overlay))
		{
			return;
		}

		int loadedThisFrame = 0;
		foreach (RuneConfigLoadTarget target in state.LoadTargets)
		{
			if (!GodotObject.IsInstanceValid(overlay) || !overlay.IsInsideTree())
			{
				return;
			}

			RuneIconBinding binding = CreateRuneIcon(target.Entry, target.PendingDisabledIds, state.UpdateSummary);
			if (ReferenceEquals(target.PendingDisabledIds, state.PendingDisabledPlayerIds))
			{
				state.PlayerIconBindings.Add(binding);
			}
			else if (ReferenceEquals(target.PendingDisabledIds, state.PendingDisabledMonsterHexIds))
			{
				state.EnemyIconBindings.Add(binding);
			}
			else
			{
				state.ForgeIconBindings.Add(binding);
			}
			target.Grid.AddChild(binding.Root);
			WireControllerFocusScrolling(binding.Root);

			loadedThisFrame++;
			if (loadedThisFrame < RuneConfigIconsPerFrame)
			{
				continue;
			}

			loadedThisFrame = 0;
			if (!await HextechGodotAsync.AwaitProcessFrameAsync(overlay))
			{
				return;
			}
		}
	}

	private static Label CreateRuneNameLabel(string text)
	{
		Label label = CreateLabel(text, 13, new Color(0.96f, 0.97f, 1f, 1f));
		label.CustomMinimumSize = new Vector2(108f, 38f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Top;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.ClipText = true;
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
		label.AddThemeConstantOverride("outline_size", 2);
		return label;
	}

	private static void AttachRuneToggleInput(
		Control root,
		RuneConfigEntry entry,
		RuneIconBinding binding,
		HashSet<string> pendingDisabledIds,
		Action updateSummary)
	{
		bool pointerPressed = false;
		bool pointerDragged = false;
		bool longPressShown = false;
		int pointerToken = 0;
		Vector2 pressPosition = Vector2.Zero;

		root.GuiInput += inputEvent =>
		{
			if (inputEvent.IsActionPressed("ui_accept"))
			{
				root.GetViewport()?.SetInputAsHandled();
				ToggleRune(entry.Id, binding, pendingDisabledIds, updateSummary);
				return;
			}

			switch (inputEvent)
			{
				case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton:
					if (mouseButton.Pressed)
					{
						BeginRunePress(mouseButton.Position, false);
					}
					else
					{
						EndRunePress();
					}
					break;
				case InputEventMouseMotion mouseMotion when pointerPressed:
					UpdateRuneDrag(mouseMotion.Position);
					break;
				case InputEventScreenTouch screenTouch:
					if (screenTouch.Pressed)
					{
						BeginRunePress(screenTouch.Position, true);
					}
					else
					{
						EndRunePress();
					}
					break;
				case InputEventScreenDrag screenDrag when pointerPressed:
					UpdateRuneDrag(screenDrag.Position);
					break;
			}
		};

		void BeginRunePress(Vector2 position, bool touch)
		{
			pointerPressed = true;
			pointerDragged = false;
			longPressShown = false;
			pressPosition = position;
			pointerToken++;
			if (touch)
			{
				int currentToken = pointerToken;
				TaskHelper.RunSafely(ShowTouchHoverTipAfterDelay(root, entry.Relic, GetEnemyHexKind(entry), currentToken, () => pointerToken == currentToken && pointerPressed && !pointerDragged, () => longPressShown = true));
			}
		}

		void UpdateRuneDrag(Vector2 position)
		{
			if (pressPosition.DistanceTo(position) <= RuneConfigDragThreshold)
			{
				return;
			}

			pointerDragged = true;
			NHoverTipSet.Remove(root);
		}

		void EndRunePress()
		{
			if (!pointerPressed)
			{
				return;
			}

			pointerPressed = false;
			pointerToken++;
			if (!pointerDragged && !longPressShown)
			{
				root.GetViewport()?.SetInputAsHandled();
				ToggleRune(entry.Id, binding, pendingDisabledIds, updateSummary);
			}
			else if (longPressShown)
			{
				root.GetViewport()?.SetInputAsHandled();
			}

			NHoverTipSet.Remove(root);
		}
	}

	private static async Task ShowTouchHoverTipAfterDelay(
		Control holder,
		RelicModel relic,
		MonsterHexKind? monsterHex,
		int token,
		Func<bool> shouldShow,
		Action onShown)
	{
		if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree())
		{
			return;
		}

		SceneTree tree = holder.GetTree();
		if (tree == null)
		{
			return;
		}

		await holder.ToSignal(tree.CreateTimer(RuneConfigLongPressSeconds), "timeout");
		if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !shouldShow())
		{
			return;
		}

		ShowRelicHoverTips(holder, relic, monsterHex);
		onShown();
		holder.GetViewport()?.SetInputAsHandled();
	}

	private static Button CreateActionButton(string text, Action action, bool compactLayout = false)
	{
		Button button = new()
		{
			Text = string.Empty,
			FocusMode = Control.FocusModeEnum.All,
			CustomMinimumSize = compactLayout ? new Vector2(112f, 34f) : new Vector2(132f, 38f),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.1f, 0.12f, 0.17f, 0.9f), new Color(0.46f, 0.55f, 0.68f, 0.78f)));
		button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.13f, 0.16f, 0.22f, 0.95f), new Color(0.88f, 0.72f, 0.36f, 0.92f)));
		button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.07f, 0.09f, 0.13f, 0.98f), new Color(0.88f, 0.62f, 0.28f, 0.92f)));
		button.AddThemeStyleboxOverride("focus", CreateButtonStyle(new Color(0.13f, 0.16f, 0.22f, 0.95f), new Color(0.88f, 0.72f, 0.36f, 0.92f)));
		AddCrispButtonText(button, text, compactLayout ? 14 : 16, new Color(0.96f, 0.94f, 0.88f, 1f));
		button.Pressed += action;
		return button;
	}

	private static void UpdateAllRuneIcons(IReadOnlyList<RuneIconBinding> bindings, IReadOnlySet<string> pendingDisabledIds)
	{
		foreach (RuneIconBinding binding in bindings)
		{
			ApplyRuneIconState(binding, !pendingDisabledIds.Contains(binding.Id));
		}
	}

	private static void ApplyRuneIconState(RuneIconBinding binding, bool enabled, bool animated = false)
	{
		Color holderTarget = enabled
			? Colors.White
			: new Color(0.34f, 0.36f, 0.4f, 0.44f);
		Color titleTarget = enabled
			? Colors.White
			: new Color(0.6f, 0.64f, 0.72f, 0.58f);

		if (!animated || !GodotObject.IsInstanceValid(binding.Root) || !binding.Root.IsInsideTree())
		{
			binding.Holder.Modulate = holderTarget;
			binding.Title.Modulate = titleTarget;
			return;
		}

		Tween tween = binding.Root.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(binding.Holder, "modulate", holderTarget, RuneStateFadeSeconds).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(binding.Title, "modulate", titleTarget, RuneStateFadeSeconds).SetEase(Tween.EaseType.Out);
	}

	private static void ToggleRune(string id, RuneIconBinding binding, HashSet<string> pendingDisabledIds, Action updateSummary)
	{
		if (pendingDisabledIds.Contains(id))
		{
			pendingDisabledIds.Remove(id);
		}
		else
		{
			pendingDisabledIds.Add(id);
		}

		ApplyRuneIconState(binding, !pendingDisabledIds.Contains(id), animated: true);
		PlayRuneToggleFeedback(binding.Root);
		updateSummary();
	}

	private static void PlayRuneToggleFeedback(Control root)
	{
		if (!GodotObject.IsInstanceValid(root))
		{
			return;
		}

		root.PivotOffset = root.Size * 0.5f;
		Tween tween = root.CreateTween();
		tween.TweenProperty(root, "scale", Vector2.One * 1.06f, 0.055f);
		tween.TweenProperty(root, "scale", Vector2.One, 0.085f);
	}

	private static void AttachRelicHoverTips(Control holder, RelicModel relic, MonsterHexKind? monsterHex = null)
	{
		holder.MouseEntered += () => ShowRelicHoverTips(holder, relic, monsterHex);
		holder.MouseExited += () => NHoverTipSet.Remove(holder);
		holder.FocusEntered += () => ShowRelicHoverTips(holder, relic, monsterHex);
		holder.FocusExited += () => NHoverTipSet.Remove(holder);
		holder.TreeExiting += () => NHoverTipSet.Remove(holder);
	}

	private static void ConfigureHorizontalFocus(IReadOnlyList<Button> buttons)
	{
		for (int i = 0; i < buttons.Count; i++)
		{
			buttons[i].FocusNeighborLeft = buttons[Math.Max(0, i - 1)].GetPath();
			buttons[i].FocusNeighborRight = buttons[Math.Min(buttons.Count - 1, i + 1)].GetPath();
		}
	}

	private static void WireControllerFocusScrolling(Node node)
	{
		if (node is Control control && control.FocusMode != Control.FocusModeEnum.None && !control.HasMeta("hextech_focus_scroll"))
		{
			control.SetMeta("hextech_focus_scroll", true);
			control.FocusEntered += () =>
			{
				FindAncestor<ScrollContainer>(control)?.EnsureControlVisible(control);
			};
		}

		foreach (Node child in node.GetChildren())
		{
			WireControllerFocusScrolling(child);
		}
	}

	private static void ShowRelicHoverTips(Control holder, RelicModel relic, MonsterHexKind? monsterHex = null)
	{
		NHoverTipSet.Remove(holder);
		IEnumerable<IHoverTip> hoverTips = monsterHex.HasValue
			? MonsterHexCatalog.GetEnemyHexHoverTips(monsterHex.Value)
			: relic.HoverTips;
		NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(holder, hoverTips, HoverTip.GetHoverTipAlignment(holder));
		if (hoverTipSet == null)
		{
			return;
		}

		hoverTipSet.ZIndex = HoverTipZIndex;
		hoverTipSet.ZAsRelative = false;
		hoverTipSet.SetAlignment(holder, HoverTip.GetHoverTipAlignment(holder));
	}

	private static MonsterHexKind? GetEnemyHexKind(RuneConfigEntry entry)
	{
		return entry.PoolKey == "ENEMY" && Enum.TryParse(entry.Id, out MonsterHexKind monsterHex)
			? monsterHex
			: null;
	}

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

	private sealed record RuneConfigEntry(
		string Id,
		RelicModel Relic,
		string Title,
		string RarityText,
		string PoolText,
		string TagText,
		int RarityOrder,
		string PoolKey,
		string TagKey,
		string SourceKey,
		string SourceText);

	private sealed record RuneConfigLoadTarget(
		RuneConfigEntry Entry,
		Container Grid,
		HashSet<string> PendingDisabledIds);

	private sealed record RuneConfigOverlayState(
		IReadOnlyList<RuneConfigLoadTarget> LoadTargets,
		HashSet<string> PendingDisabledPlayerIds,
		HashSet<string> PendingDisabledMonsterHexIds,
		HashSet<string> PendingDisabledForgeIds,
		List<RuneIconBinding> PlayerIconBindings,
		List<RuneIconBinding> EnemyIconBindings,
		List<RuneIconBinding> ForgeIconBindings,
		Control InitialFocus,
		IReadOnlyList<Button> TabButtons,
		Action UpdateSummary);

	private sealed record RuneIconBinding(
		string Id,
		Control Root,
		Control Holder,
		Label Title);

	private sealed record NumericValueBinding(
		Func<string> GetText,
		Label Number);

	private sealed record BooleanValueBinding(
		Func<bool> GetValue,
		BaseButton Toggle,
		Action<bool>? ApplyVisual = null);

}
