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

	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(NTopBarPortraitTip), "Initialize", BindingFlags.Instance | BindingFlags.Public, typeof(IRunState)),
			postfix: new HarmonyMethod(typeof(HextechPlayerStatsHoverHooks), nameof(InitializePostfix)));
		harmony.Patch(
			RequireMethod(typeof(NTopBarPortraitTip), "OnFocus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
			prefix: new HarmonyMethod(typeof(HextechPlayerStatsHoverHooks), nameof(OnFocusPrefix)));
	}

	private static void InitializePostfix(NTopBarPortraitTip __instance, IRunState runState)
	{
		UpdatePortraitTip(__instance, runState);
	}

	private static void OnFocusPrefix(NTopBarPortraitTip __instance)
	{
		UpdatePortraitTip(__instance, RunManager.Instance.DebugOnlyGetState());
	}

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
}
