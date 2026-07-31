using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

public sealed class HextechSilverRunModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_SILVER_RUN.title");

	public override LocString Description => new("modifiers", "HEXTECH_SILVER_RUN.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/silverForge.png";
}

public sealed class HextechGoldRunModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_GOLD_RUN.title");

	public override LocString Description => new("modifiers", "HEXTECH_GOLD_RUN.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/goldForge.png";
}

public sealed class HextechPrismaticRunModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_PRISMATIC_RUN.title");

	public override LocString Description => new("modifiers", "HEXTECH_PRISMATIC_RUN.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/prismaticForge.png";
}

internal static class HextechCustomRunModifierHooks
{
	// 清单本体在 Content/HextechCustomModelRegistry(注册表层),此处仅取用。
	private static IReadOnlyList<Type> CustomRarityModifierTypes => HextechCustomModelRegistry.CustomRarityModifierTypes;

	private static readonly FieldInfo? ModifierTickboxesField = TryGetField(typeof(NCustomRunModifiersList), "_modifierTickboxes");
	private static readonly FieldInfo? ModifiersContainerField = TryGetField(typeof(NCustomRunModifiersList), "_container");
	private static readonly FieldInfo? RunModifierLabelField = TryGetField(typeof(NRunModifierTickbox), "_label");

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
				postfix: new HarmonyMethod(typeof(HextechCustomRunModifierHooks), nameof(AppendCustomRarityModifiersPostfix)));
		}
		else if (TryGetMethod(
			typeof(NCustomRunModifiersList),
			nameof(NCustomRunModifiersList._Ready),
			BindingFlags.Instance | BindingFlags.Public,
			warnIfMissing: false) is { } readyMethod)
		{
			harmony.Patch(
				readyMethod,
				postfix: new HarmonyMethod(typeof(HextechCustomRunModifierHooks), nameof(AppendCustomRarityModifierTickboxesPostfix)));
			Log.Warn($"[{ModInfo.Id}][CustomRun] NCustomRunModifiersList.GetAllModifiers not found; using _Ready fallback for custom rarity modifiers.");
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not install custom rarity modifier list hook; no compatible NCustomRunModifiersList entrypoint found.");
		}

		if (TryGetMethod(
			typeof(NCustomRunModifiersList),
			"UntickMutuallyExclusiveModifiersForTickbox",
			BindingFlags.Instance | BindingFlags.NonPublic,
			warnIfMissing: false,
			typeof(NRunModifierTickbox)) is { } untickMethod)
		{
			harmony.Patch(
				untickMethod,
				postfix: new HarmonyMethod(typeof(HextechCustomRunModifierHooks), nameof(UntickOtherHextechRarityModifiersPostfix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not install mutual exclusion hook for custom rarity modifiers.");
		}

		if (TryGetMethod(
			typeof(NRunModifierTickbox),
			nameof(NRunModifierTickbox._Ready),
			BindingFlags.Instance | BindingFlags.Public,
			warnIfMissing: false) is { } tickboxReadyMethod)
		{
			harmony.Patch(
				tickboxReadyMethod,
				postfix: new HarmonyMethod(typeof(HextechCustomRunModifierHooks), nameof(ColorCustomRarityModifierTickboxPostfix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not install custom rarity modifier label color hook.");
		}
	}

	public static HextechRarityTier? GetForcedRarity(RunState runState)
	{
		foreach (ModifierModel modifier in runState.Modifiers)
		{
			if (TryGetForcedRarity(modifier, out HextechRarityTier rarity))
			{
				return rarity;
			}
		}

		return null;
	}

	private static void AppendCustomRarityModifiersPostfix(ref IEnumerable<ModifierModel> __result)
	{
		__result = __result.Concat(CreateCustomRarityModifiers());
	}

	private static void AppendCustomRarityModifierTickboxesPostfix(NCustomRunModifiersList __instance)
	{
		if (ModifierTickboxesField?.GetValue(__instance) is not IList<NRunModifierTickbox> tickboxes)
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not read custom run modifier tickbox list; custom rarity modifiers were not added.");
			return;
		}

		Control? container = ModifiersContainerField?.GetValue(__instance) as Control
			?? __instance.GetNodeOrNull<Control>("ScrollContainer/Mask/Content");
		if (container == null)
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not find custom run modifier container; custom rarity modifiers were not added.");
			return;
		}

		foreach (ModifierModel modifier in CreateCustomRarityModifiers())
		{
			if (tickboxes.Any(tickbox => tickbox.Modifier?.GetType() == modifier.GetType()))
			{
				continue;
			}

			NRunModifierTickbox? tickbox = NRunModifierTickbox.Create(modifier);
			if (tickbox == null)
			{
				continue;
			}

			container.AddChild(tickbox);
			tickboxes.Add(tickbox);
			tickbox.Connect(NTickbox.SignalName.Toggled, Callable.From<NTickbox>(_ => OnCustomRarityModifierToggled(__instance, tickbox)));
		}
	}

	private static void OnCustomRarityModifierToggled(NCustomRunModifiersList list, NRunModifierTickbox tickbox)
	{
		UntickOtherHextechRarityModifiersPostfix(list, tickbox);
		list.EmitSignal(NCustomRunModifiersList.SignalName.ModifiersChanged);
	}

	private static IEnumerable<ModifierModel> CreateCustomRarityModifiers()
	{
		yield return ModelDb.Modifier<HextechSilverRunModifier>().ToMutable();
		yield return ModelDb.Modifier<HextechGoldRunModifier>().ToMutable();
		yield return ModelDb.Modifier<HextechPrismaticRunModifier>().ToMutable();
	}

	private static void UntickOtherHextechRarityModifiersPostfix(NCustomRunModifiersList __instance, NRunModifierTickbox tickbox)
	{
		if (!tickbox.IsTicked || tickbox.Modifier == null || !IsCustomRarityModifier(tickbox.Modifier))
		{
			return;
		}

		if (ModifierTickboxesField?.GetValue(__instance) is not IEnumerable<NRunModifierTickbox> tickboxes)
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not read custom run modifier tickboxes; Hextech rarity modifiers may not untick each other.");
			return;
		}

		foreach (NRunModifierTickbox otherTickbox in tickboxes)
		{
			if (!ReferenceEquals(otherTickbox, tickbox)
				&& otherTickbox.Modifier != null
				&& IsCustomRarityModifier(otherTickbox.Modifier))
			{
				otherTickbox.IsTicked = false;
			}
		}
	}

	private static void ColorCustomRarityModifierTickboxPostfix(NRunModifierTickbox __instance)
	{
		if (__instance.Modifier == null || !IsCustomRarityModifier(__instance.Modifier))
		{
			return;
		}

		if (RunModifierLabelField?.GetValue(__instance) is not MegaRichTextLabel label)
		{
			Log.Warn($"[{ModInfo.Id}][CustomRun] Could not recolor custom run modifier label.");
			return;
		}

		LocString labelLoc = new("main_menu_ui", "CUSTOM_RUN_SCREEN.MODIFIER_LABEL");
		labelLoc.Add("color", "green");
		labelLoc.Add("modifier_title", __instance.Modifier.Title.GetFormattedText());
		labelLoc.Add("modifier_description", __instance.Modifier.Description.GetFormattedText());
		label.Text = labelLoc.GetFormattedText();
	}

	private static bool IsCustomRarityModifier(ModifierModel modifier)
	{
		return TryGetForcedRarity(modifier, out _);
	}

	private static bool TryGetForcedRarity(ModifierModel modifier, out HextechRarityTier rarity)
	{
		switch (modifier)
		{
			case HextechSilverRunModifier:
				rarity = HextechRarityTier.Silver;
				return true;
			case HextechGoldRunModifier:
				rarity = HextechRarityTier.Gold;
				return true;
			case HextechPrismaticRunModifier:
				rarity = HextechRarityTier.Prismatic;
				return true;
			default:
				rarity = default;
				return false;
		}
	}
}
