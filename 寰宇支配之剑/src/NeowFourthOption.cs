using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace UniversalDominionSword;

internal static class NeowFourthOption
{
	private const string PositiveDonePage = "NEOW.pages.DONE.POSITIVE.description";

	private static readonly MethodInfo? GenerateInitialOptionsMethod =
		AccessTools.DeclaredMethod(typeof(Neow), "GenerateInitialOptions");

	private static readonly MethodInfo? AllPossibleOptionsGetter =
		AccessTools.PropertyGetter(typeof(Neow), nameof(Neow.AllPossibleOptions));

	private static readonly MethodInfo? RelicOptionMethod =
		AccessTools.Method(
			typeof(AncientEventModel),
			"RelicOption",
			[typeof(RelicModel), typeof(string), typeof(string)]);

	public static void Install(Harmony harmony)
	{
		if (GenerateInitialOptionsMethod == null
			|| AllPossibleOptionsGetter == null
			|| RelicOptionMethod == null)
		{
			throw new MissingMethodException(
				$"Could not find the STS2 Neow methods required by {nameof(NeowFourthOption)}.");
		}

		harmony.Patch(
			GenerateInitialOptionsMethod,
			postfix: new HarmonyMethod(
				typeof(NeowFourthOption),
				nameof(GenerateInitialOptionsPostfix)));

		harmony.Patch(
			AllPossibleOptionsGetter,
			postfix: new HarmonyMethod(
				typeof(NeowFourthOption),
				nameof(AllPossibleOptionsPostfix)));
	}

	private static void GenerateInitialOptionsPostfix(
		Neow __instance,
		ref IReadOnlyList<EventOption> __result)
	{
		var owner = __instance.Owner;
		if (owner == null
			|| owner.RunState.Modifiers.Count > 0
			|| ContainsSwordOption(__result))
		{
			return;
		}

		List<EventOption> options = __result.ToList();
		options.Add(CreateSwordOption(__instance));
		__result = options;

		Log.Info(
			$"[{ModInfo.Id}] Added the Universal Dominion Sword as Neow's fixed fourth option.");
	}

	private static void AllPossibleOptionsPostfix(
		Neow __instance,
		ref IEnumerable<EventOption> __result)
	{
		if (ContainsSwordOption(__result))
		{
			return;
		}

		List<EventOption> options = __result.ToList();
		options.Add(CreateSwordOption(__instance));
		__result = options;
	}

	private static EventOption CreateSwordOption(Neow neow)
	{
		RelicModel relic = ModelDb.Relic<UniversalDominionSwordRelic>().ToMutable();
		object? result = RelicOptionMethod!.Invoke(
			neow,
			[relic, "INITIAL", PositiveDonePage]);

		return result as EventOption
			?? throw new InvalidOperationException(
				"STS2 did not create the Neow relic option expected by Universal Dominion Sword.");
	}

	private static bool ContainsSwordOption(IEnumerable<EventOption> options)
	{
		return options.Any(option => option.Relic is UniversalDominionSwordRelic);
	}
}
