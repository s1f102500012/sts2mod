using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.sts2.Core.Nodes.TopBar;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechPlayerStatsHoverHooks
{
	private const string LocTable = "relic_collection";
	private static string HealthLabel => new LocString(LocTable, "HEXTECH_STAT_COEFF_HEALTH").GetRawText();
	private static string DamageLabel => new LocString(LocTable, "HEXTECH_STAT_COEFF_DAMAGE").GetRawText();
	private static string BlockLabel => new LocString(LocTable, "HEXTECH_STAT_COEFF_BLOCK").GetRawText();
	private static string HealingLabel => new LocString(LocTable, "HEXTECH_STAT_COEFF_HEALING").GetRawText();

	private static readonly FieldInfo PortraitHoverTipField = RequireField(typeof(NTopBarPortraitTip), "_hoverTip");
	private static readonly FieldInfo HoverTipDescriptionField = RequireField(typeof(HoverTip), "<Description>k__BackingField");


	private static void UpdatePortraitTip(NTopBarPortraitTip portraitTip, IRunState? runState)
	{
		try
		{
			if (runState is not RunState concreteRunState || concreteRunState.Players.Count == 0)
			{
				return;
			}

			Player player = concreteRunState.Players[0];
			if (PortraitHoverTipField.GetValue(portraitTip) is not HoverTip hoverTip)
			{
				return;
			}

			object boxedHoverTip = hoverTip;
			HoverTipDescriptionField.SetValue(
				boxedHoverTip,
				BuildDescription(RemoveExistingCoefficientLines(hoverTip.Description), player));
			PortraitHoverTipField.SetValue(portraitTip, boxedHoverTip);
		}
		catch (Exception ex)
		{
			if (HextechRunLogBudget.TryConsume("ui.player-stats-hover-update-failure", 3))
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Failed to update portrait stat hover tip: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	private static string BuildDescription(string baseDescription, Player player)
	{
		HextechPlayerCoefficients coefficients = HextechPlayerCoefficientHelper.Get(player);
		return string.Join(
			'\n',
			[
				baseDescription.TrimEnd(),
				$"{HealthLabel}{HextechPlayerCoefficientHelper.FormatPercent(coefficients.Health)}",
				$"{DamageLabel}{HextechPlayerCoefficientHelper.FormatPercent(coefficients.Damage)}",
				$"{BlockLabel}{HextechPlayerCoefficientHelper.FormatPercent(coefficients.Block)}",
				$"{HealingLabel}{HextechPlayerCoefficientHelper.FormatPercent(coefficients.Healing)}"
			]);
	}

	private static string RemoveExistingCoefficientLines(string description)
	{
		string[] lines = description.Replace("\r\n", "\n").Split('\n');
		int end = lines.Length;
		while (end > 0 && IsCoefficientLine(lines[end - 1]))
		{
			end--;
		}

		return string.Join('\n', lines.Take(end));
	}

	private static bool IsCoefficientLine(string line)
	{
		string trimmed = line.TrimStart();
		return trimmed.StartsWith(HealthLabel, StringComparison.Ordinal)
			|| trimmed.StartsWith(DamageLabel, StringComparison.Ordinal)
			|| trimmed.StartsWith(BlockLabel, StringComparison.Ordinal)
			|| trimmed.StartsWith(HealingLabel, StringComparison.Ordinal);
	}

	[HarmonyPatch(typeof(NTopBarPortraitTip), "Initialize", typeof(IRunState))]
	[HextechPatch("ui.player-stats-hover.init", "玩家属性悬浮")]
	private static class InitializePatch
	{
		[HarmonyPostfix]
		private static void Postfix(NTopBarPortraitTip __instance, IRunState runState)
		{
			UpdatePortraitTip(__instance, runState);
		}
	}

	[HarmonyPatch(typeof(NTopBarPortraitTip), "OnFocus", new Type[0])]
	[HextechPatch("ui.player-stats-hover.focus", "玩家属性悬浮")]
	private static class OnFocusPatch
	{
		[HarmonyPrefix]
		private static void Prefix(NTopBarPortraitTip __instance)
		{
			UpdatePortraitTip(__instance, RunManager.Instance.DebugOnlyGetState());
		}
	}
}
