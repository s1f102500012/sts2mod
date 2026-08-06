using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

public sealed class StuffedToRuinChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_STUFFED_TO_RUIN_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_STUFFED_TO_RUIN_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/stuffedToRuinChallenge.png";
}

public sealed class DefenseCounterMasterChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_DEFENSE_COUNTER_MASTER_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_DEFENSE_COUNTER_MASTER_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/defenseCounterMasterChallenge.png";
}

public sealed class BruteForceChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_BRUTE_FORCE_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_BRUTE_FORCE_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/bruteForceChallenge.png";
}

public sealed class EightPennyGateChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_EIGHT_PENNY_GATE_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_EIGHT_PENNY_GATE_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/eightPennyGateChallenge.png";
}

public sealed class ListlessChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_LISTLESS_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_LISTLESS_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/listlessChallenge.png";
}

internal sealed record HextechPresetChallengeActPlan(
	HextechRarityTier PlayerRarity,
	IReadOnlyList<MonsterHexKind> EnemyHexes);

internal static class HextechPresetChallengeRegistry
{
	private static readonly IReadOnlyList<HextechPresetChallengeActPlan> StuffedToRuinActs =
	[
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.ForgottenSoul ]),
		new(HextechRarityTier.Gold, [ MonsterHexKind.PhrogParasite, MonsterHexKind.ManipulateReality ]),
		new(HextechRarityTier.Silver, [ MonsterHexKind.LeafSlime, MonsterHexKind.DizzySpinning ])
	];

	private static readonly IReadOnlyList<HextechPresetChallengeActPlan> DefenseCounterMasterActs =
	[
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.Exoskeleton ]),
		new(HextechRarityTier.Gold, [ MonsterHexKind.HundredRefinements, MonsterHexKind.Porcupine ]),
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.ProteinShake, MonsterHexKind.UnmovableMountain ])
	];

	private static readonly IReadOnlyList<HextechPresetChallengeActPlan> BruteForceActs =
	[
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.Goliath ]),
		new(HextechRarityTier.Gold, [ MonsterHexKind.AstralBody, MonsterHexKind.VitalitySurge ]),
		new(HextechRarityTier.Gold, [ MonsterHexKind.StatsOnStats, MonsterHexKind.TankEngine ])
	];

	private static readonly IReadOnlyList<HextechPresetChallengeActPlan> EightPennyGateActs =
	[
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.EightPennyGate ]),
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.IGrip ]),
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.IInspect ])
	];

	private static readonly IReadOnlyList<HextechPresetChallengeActPlan> ListlessActs =
	[
		new(HextechRarityTier.Gold, [ MonsterHexKind.MonarchsGaze ]),
		new(HextechRarityTier.Silver, [ MonsterHexKind.TheLost, MonsterHexKind.TheForgotten ]),
		new(HextechRarityTier.Prismatic, [ MonsterHexKind.LagavulinMatriarch, MonsterHexKind.MasterOfDuality ])
	];

	internal static bool IsActive(RunState runState)
	{
		return runState.Modifiers.Any(static modifier => IsChallengeModifierType(modifier.GetType()));
	}

	internal static bool TryGetActPlan(RunState runState, int actIndex, out HextechPresetChallengeActPlan plan)
	{
		foreach (ModifierModel modifier in runState.Modifiers)
		{
			if (TryGetActPlan(modifier.GetType(), actIndex, out plan))
			{
				return true;
			}
		}

		plan = null!;
		return false;
	}

	internal static bool TryGetActPlan(Type modifierType, int actIndex, out HextechPresetChallengeActPlan plan)
	{
		IReadOnlyList<HextechPresetChallengeActPlan>? acts = modifierType == typeof(StuffedToRuinChallengeModifier)
			? StuffedToRuinActs
			: modifierType == typeof(DefenseCounterMasterChallengeModifier)
				? DefenseCounterMasterActs
				: modifierType == typeof(BruteForceChallengeModifier)
					? BruteForceActs
					: modifierType == typeof(EightPennyGateChallengeModifier)
						? EightPennyGateActs
						: modifierType == typeof(ListlessChallengeModifier)
							? ListlessActs
							: null;
		if (acts != null && (uint)actIndex < (uint)acts.Count)
		{
			plan = acts[actIndex];
			return true;
		}

		plan = null!;
		return false;
	}

	internal static bool IsChallengeModifierType(Type modifierType)
	{
		return modifierType == typeof(StuffedToRuinChallengeModifier)
			|| modifierType == typeof(DefenseCounterMasterChallengeModifier)
			|| modifierType == typeof(BruteForceChallengeModifier)
			|| modifierType == typeof(EightPennyGateChallengeModifier)
			|| modifierType == typeof(ListlessChallengeModifier);
	}

	internal static bool AreMutuallyExclusiveChallengeTypes(Type selectedType, Type candidateType)
	{
		return selectedType != candidateType
			&& IsChallengeModifierType(selectedType)
			&& IsChallengeModifierType(candidateType);
	}
}

internal static class HextechPresetChallengeHooks
{
	public static void Install(Harmony harmony)
	{
		MethodInfo? getAllModifiers = TryGetMethod(
			typeof(NCustomRunModifiersList),
			"GetAllModifiers",
			BindingFlags.Instance | BindingFlags.NonPublic,
			warnIfMissing: false);
		if (getAllModifiers != null)
		{
			harmony.Patch(
				getAllModifiers,
				postfix: new HarmonyMethod(typeof(HextechPresetChallengeHooks), nameof(AppendPresetChallengesPostfix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not install preset challenge option; NCustomRunModifiersList.GetAllModifiers was not found.");
		}

		MethodInfo? untickMutuallyExclusive = TryGetMethod(
			typeof(NCustomRunModifiersList),
			"UntickMutuallyExclusiveModifiersForTickbox",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(NRunModifierTickbox));
		if (untickMutuallyExclusive != null)
		{
			harmony.Patch(
				untickMutuallyExclusive,
				postfix: new HarmonyMethod(typeof(HextechPresetChallengeHooks), nameof(UntickOtherPresetChallengesPostfix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not install preset challenge exclusivity; NCustomRunModifiersList.UntickMutuallyExclusiveModifiersForTickbox was not found.");
		}
	}

	private static void AppendPresetChallengesPostfix(ref IEnumerable<ModifierModel> __result)
	{
		__result = __result.Concat(CreatePresetChallenges());
	}

	private static IEnumerable<ModifierModel> CreatePresetChallenges()
	{
		yield return ModelDb.Modifier<StuffedToRuinChallengeModifier>().ToMutable();
		yield return ModelDb.Modifier<DefenseCounterMasterChallengeModifier>().ToMutable();
		yield return ModelDb.Modifier<BruteForceChallengeModifier>().ToMutable();
		yield return ModelDb.Modifier<EightPennyGateChallengeModifier>().ToMutable();
		yield return ModelDb.Modifier<ListlessChallengeModifier>().ToMutable();
	}

	private static void UntickOtherPresetChallengesPostfix(
		NRunModifierTickbox tickbox,
		List<NRunModifierTickbox> ____modifierTickboxes)
	{
		ModifierModel? selectedModifier = tickbox.Modifier;
		if (!tickbox.IsTicked
			|| selectedModifier == null
			|| !HextechPresetChallengeRegistry.IsChallengeModifierType(selectedModifier.GetType()))
		{
			return;
		}

		foreach (NRunModifierTickbox otherTickbox in ____modifierTickboxes)
		{
			ModifierModel? otherModifier = otherTickbox.Modifier;
			if (otherModifier != null
				&& HextechPresetChallengeRegistry.AreMutuallyExclusiveChallengeTypes(
					selectedModifier.GetType(),
					otherModifier.GetType()))
			{
				otherTickbox.IsTicked = false;
			}
		}
	}
}
