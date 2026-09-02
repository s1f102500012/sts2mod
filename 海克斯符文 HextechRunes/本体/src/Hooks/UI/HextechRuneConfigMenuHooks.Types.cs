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
