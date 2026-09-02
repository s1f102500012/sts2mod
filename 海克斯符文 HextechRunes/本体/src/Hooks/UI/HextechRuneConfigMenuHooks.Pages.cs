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
}
