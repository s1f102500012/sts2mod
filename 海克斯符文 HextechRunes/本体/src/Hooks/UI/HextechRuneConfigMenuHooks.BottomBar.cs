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
}
