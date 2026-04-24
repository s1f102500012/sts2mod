using System.Reflection;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.sts2.Core.Nodes.TopBar;

namespace HextechRunes;

internal static class HextechEnemyUi
{
	private const string EnemyHexRootName = "HextechEnemyHexStrip";
	private const string EnemyHexRowName = "HextechEnemyHexStripRow";
	private const int EnemyHexContentOffsetX = -12;
	private const float EnemyHexHolderSize = 42f;
	private const int EnemyHexStripHorizontalMargin = 24;
	private const int EnemyHexStripMarginTop = 2;
	private const int EnemyHexStripMarginBottom = 2;
	private const int EnemyHexSeparation = 30;

	private static readonly FieldInfo ModifiersContainerField =
		typeof(NTopBar).GetField("_modifiersContainer", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NTopBar._modifiersContainer.");

	private static readonly FieldInfo TopBarModifierModelField =
		typeof(NTopBarModifier).GetField("_modifier", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NTopBarModifier._modifier.");

	public static void Refresh(HextechMayhemModifier modifier)
	{
		Control? container = GetModifiersContainer();
		if (container == null)
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] EnemyUi.Refresh: no modifiers container");
			return;
		}

		HideMayhemModifierBadge();

		IReadOnlyList<MonsterHexKind> activeHexes = modifier.GetActiveMonsterHexes();
		if (activeHexes.Count == 0)
		{
			RemoveAllEnemyHexStrips(container);
			UpdateContainerVisibility(container);
			Log.Info($"[{ModInfo.Id}][Mayhem] EnemyUi.Refresh: no active enemy hexes");
			return;
		}
		Log.Info($"[{ModInfo.Id}][Mayhem] EnemyUi.Refresh: active={string.Join(",", activeHexes)}");

		HBoxContainer strip = GetOrCreateStrip(container);
		RebuildStrip(strip, activeHexes);
		UpdateContainerVisibility(container);
	}

	public static void Clear()
	{
		Control? container = GetModifiersContainer();
		if (container == null)
		{
			return;
		}

		RemoveAllEnemyHexStrips(container);
		HideMayhemModifierBadge();
		UpdateContainerVisibility(container);
	}

	public static void HideMayhemModifierBadge()
	{
		Control? container = GetModifiersContainer();
		if (container == null)
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] EnemyUi.HideMayhemModifierBadge: no modifiers container");
			return;
		}

		foreach (Node child in container.GetChildren())
		{
			if (child is NTopBarModifier topBarModifier
				&& TopBarModifierModelField.GetValue(topBarModifier) is HextechMayhemModifier)
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] EnemyUi.HideMayhemModifierBadge: removed top bar modifier badge");
				topBarModifier.QueueFree();
			}
		}
	}

	private static Control? GetModifiersContainer()
	{
		NTopBar? topBar = NRun.Instance?.GlobalUi?.TopBar;
		return topBar == null ? null : ModifiersContainerField.GetValue(topBar) as Control;
	}

	private static HBoxContainer GetOrCreateStrip(Control container)
	{
		RemoveAllEnemyHexStrips(container);

		PanelContainer root = new()
		{
			Name = EnemyHexRootName,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		root.AddThemeStyleboxOverride("panel", CreateStripStyle());

		MarginContainer margin = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		margin.AddThemeConstantOverride("margin_left", EnemyHexStripHorizontalMargin + EnemyHexContentOffsetX);
		margin.AddThemeConstantOverride("margin_right", EnemyHexStripHorizontalMargin - EnemyHexContentOffsetX);
		margin.AddThemeConstantOverride("margin_top", EnemyHexStripMarginTop);
		margin.AddThemeConstantOverride("margin_bottom", EnemyHexStripMarginBottom);
		root.AddChild(margin);

		HBoxContainer strip = new()
		{
			Name = EnemyHexRowName,
			Alignment = BoxContainer.AlignmentMode.Begin,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		strip.AddThemeConstantOverride("separation", EnemyHexSeparation);
		margin.AddChild(strip);

		container.AddChild(root);
		container.MoveChild(root, container.GetChildCount() - 1);
		return strip;
	}

	private static void RebuildStrip(HBoxContainer strip, IReadOnlyList<MonsterHexKind> activeHexes)
	{
		foreach (Node child in strip.GetChildren())
		{
			strip.RemoveChild(child);
			child.QueueFree();
		}

		foreach (MonsterHexKind hex in activeHexes)
		{
			Control holder = CreateEnemyHexHolder(hex);
			strip.AddChild(holder);
		}
	}

	private static void RemoveAllEnemyHexStrips(Control container)
	{
		foreach (Node child in container.GetChildren())
		{
			if (child.Name == EnemyHexRootName)
			{
				container.RemoveChild(child);
				child.QueueFree();
			}
		}
	}

	private static void UpdateContainerVisibility(Control container)
	{
		container.Visible = container.GetChildren().Any(static child => !child.IsQueuedForDeletion());
	}

	private static StyleBoxFlat CreateStripStyle()
	{
		StyleBoxFlat style = new();
		style.BgColor = new Color(0.05f, 0.07f, 0.11f, 0.72f);
		style.BorderColor = new Color(0.29f, 0.35f, 0.45f, 0.55f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(14);
		style.ShadowColor = new Color(0f, 0f, 0f, 0.18f);
		style.ShadowSize = 6;
		style.ShadowOffset = new Vector2(0f, 4f);
		return style;
	}

	private static Control CreateEnemyHexHolder(MonsterHexKind hex)
	{
		RelicModel relic = ModInfo.GetIconRelicForMonsterHex(hex).ToMutable();
		Control holder = new()
		{
			Name = $"EnemyHex-{hex}",
			CustomMinimumSize = new Vector2(EnemyHexHolderSize, EnemyHexHolderSize),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			FocusMode = Control.FocusModeEnum.None
		};

		CenterContainer center = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		holder.AddChild(center);

		NRelic relicNode = NRelic.Create(relic, NRelic.IconSize.Small)
			?? throw new InvalidOperationException("Failed to create top bar enemy hex relic.");
		relicNode.MouseFilter = Control.MouseFilterEnum.Stop;
		relicNode.Scale = Vector2.One * 0.72f;
		center.AddChild(relicNode);

		relicNode.MouseEntered += () => ShowEnemyHexHoverTip(relicNode, hex);
		relicNode.MouseExited += () => NHoverTipSet.Remove(relicNode);
		return holder;
	}

	private static void ShowEnemyHexHoverTip(Control holder, MonsterHexKind hex)
	{
		NHoverTipSet.Remove(holder);
		NHoverTipSet hoverTipSet = NHoverTipSet.CreateAndShow(holder, ModInfo.GetEnemyHexHoverTips(hex));
		hoverTipSet.SetAlignment(holder, HoverTip.GetHoverTipAlignment(holder));
	}
}
