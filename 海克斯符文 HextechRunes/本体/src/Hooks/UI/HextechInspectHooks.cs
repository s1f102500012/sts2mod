using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechInspectHooks
{
	private static readonly PropertyInfo? UnlockStateRelicsProperty = typeof(UnlockState).GetProperty(nameof(UnlockState.Relics), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo? SaveManagerIsRelicSeenMethod = TryGetMethod(typeof(SaveManager), nameof(SaveManager.IsRelicSeen), BindingFlags.Instance | BindingFlags.Public, typeof(RelicModel));
	private static readonly MethodInfo? InspectRelicScreenOpenMethod = TryGetMethod(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), BindingFlags.Instance | BindingFlags.Public, typeof(IReadOnlyList<RelicModel>), typeof(RelicModel));
	private static readonly FieldInfo? InspectRelicScreenUnlockedRelicsField = TryGetField(typeof(NInspectRelicScreen), "_allUnlockedRelics");
	private static readonly FieldInfo? InspectRelicScreenRelicsField = TryGetField(typeof(NInspectRelicScreen), "_relics");
	private static readonly FieldInfo? InspectRelicScreenIndexField = TryGetField(typeof(NInspectRelicScreen), "_index");
	private static readonly FieldInfo? RelicCanonicalInstanceField = TryGetField(typeof(RelicModel), "_canonicalInstance");
	private static readonly MethodInfo? InspectRelicScreenUpdateRelicDisplayMethod = TryGetMethod(typeof(NInspectRelicScreen), "UpdateRelicDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo? InspectRelicScreenSetRelicMethod = TryGetMethod(typeof(NInspectRelicScreen), "SetRelic", BindingFlags.Instance | BindingFlags.NonPublic, typeof(int));
	private static readonly FieldInfo? InspectRelicScreenNameLabelField = TryGetField(typeof(NInspectRelicScreen), "_nameLabel");
	private static readonly FieldInfo? InspectRelicScreenRarityLabelField = TryGetField(typeof(NInspectRelicScreen), "_rarityLabel");
	private static readonly FieldInfo? InspectRelicScreenDescriptionField = TryGetField(typeof(NInspectRelicScreen), "_description");
	private static readonly FieldInfo? InspectRelicScreenFlavorField = TryGetField(typeof(NInspectRelicScreen), "_flavor");
	private static readonly FieldInfo? InspectRelicScreenImageField = TryGetField(typeof(NInspectRelicScreen), "_relicImage");
	private static readonly FieldInfo? InspectRelicScreenHoverTipRectField = TryGetField(typeof(NInspectRelicScreen), "_hoverTipRect");
	private static readonly MethodInfo? InspectRelicScreenSetRarityVisualsMethod = TryGetMethod(typeof(NInspectRelicScreen), "SetRarityVisuals", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicRarity));
	private static readonly MethodInfo? EnergyIconHelperGetPrefixMethod = TryGetMethod(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPrefix), BindingFlags.Static | BindingFlags.Public, typeof(AbstractModel));

	private static bool _inspectScreenHooksInstalled;

	private readonly record struct InspectOpenState(RelicModel? RequestedRelic);

	public static void Install(Harmony harmony)
	{
		TryPatch(harmony, UnlockStateRelicsProperty?.GetMethod, "UnlockState.Relics", postfix: nameof(GetUnlockStateRelicsPostfix));
		TryPatch(harmony, SaveManagerIsRelicSeenMethod, "SaveManager.IsRelicSeen", postfix: nameof(IsRelicSeenPostfix));
		TryPatch(harmony, EnergyIconHelperGetPrefixMethod, "EnergyIconHelper.GetPrefix", postfix: nameof(EnergyIconHelperGetPrefixPostfix));

		if (!HasInspectScreenMembers(out string missingMembers))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Inspect relic screen hooks disabled: missing {missingMembers}.");
			return;
		}

		_inspectScreenHooksInstalled = true;
		TryPatch(
			harmony,
			InspectRelicScreenOpenMethod,
			"NInspectRelicScreen.Open",
			prefix: nameof(InspectRelicScreenOpenPrefix),
			postfix: nameof(InspectRelicScreenOpenPostfix),
			prefixPriority: Priority.Last,
			postfixPriority: Priority.Last);
		TryPatch(
			harmony,
			InspectRelicScreenUpdateRelicDisplayMethod,
			"NInspectRelicScreen.UpdateRelicDisplay",
			prefix: nameof(InspectRelicScreenUpdateRelicDisplayPrefix));
	}

	private static void GetUnlockStateRelicsPostfix(ref IEnumerable<RelicModel> __result)
	{
		__result = __result.Concat(HextechCatalog.GetCanonicalVisibleCustomRelics()).Distinct();
	}

	private static void IsRelicSeenPostfix(RelicModel relic, ref bool __result)
	{
		if (HextechCatalog.IsHextechCustomRelic(relic))
		{
			__result = true;
		}
	}

	private static void InspectRelicScreenOpenPrefix(ref IReadOnlyList<RelicModel> relics, ref RelicModel relic, out InspectOpenState __state)
	{
		__state = default;
		if (!_inspectScreenHooksInstalled || !ShouldHandleInspectRequest(relic))
		{
			return;
		}

		RelicModel requestedRelic = relic;
		IReadOnlyList<RelicModel> correctedRelics = MergeRequestedInspectRelic(relics, requestedRelic, out int correctedIndex);
		relics = correctedRelics;
		relic = correctedRelics[correctedIndex];
		__state = new InspectOpenState(requestedRelic);
	}

	private static void InspectRelicScreenOpenPostfix(NInspectRelicScreen __instance, InspectOpenState __state)
	{
		if (!_inspectScreenHooksInstalled
			|| __state.RequestedRelic == null
			|| InspectRelicScreenRelicsField?.GetValue(__instance) is not IReadOnlyList<RelicModel> finalRelics)
		{
			return;
		}

		IReadOnlyList<RelicModel> mergedRelics = MergeRequestedInspectRelic(
			finalRelics,
			__state.RequestedRelic,
			out int requestedIndex);
		EnsureInspectRelicsUnlocked(__instance, mergedRelics);
		if (!ReferenceEquals(mergedRelics, finalRelics))
		{
			InspectRelicScreenRelicsField.SetValue(__instance, mergedRelics);
		}

		InspectRelicScreenSetRelicMethod?.Invoke(__instance, [requestedIndex]);
		InspectRelicScreenUpdateRelicDisplayMethod?.Invoke(__instance, null);
	}

	internal static bool ShouldHandleInspectRequest(RelicModel relic)
	{
		return HextechCatalog.IsHextechCustomRelic(relic);
	}

	internal static IReadOnlyList<RelicModel> MergeRequestedInspectRelic(
		IReadOnlyList<RelicModel> relics,
		RelicModel requestedRelic,
		out int requestedIndex)
	{
		for (int index = 0; index < relics.Count; index++)
		{
			RelicModel candidate = relics[index];
			if (candidate != null
				&& (ReferenceEquals(candidate, requestedRelic) || candidate.Id == requestedRelic.Id))
			{
				requestedIndex = index;
				return relics;
			}
		}

		List<RelicModel> merged = relics.ToList();
		merged.Add(requestedRelic);
		requestedIndex = merged.Count - 1;
		return merged;
	}

	private static bool InspectRelicScreenUpdateRelicDisplayPrefix(NInspectRelicScreen __instance)
	{
		if (!_inspectScreenHooksInstalled)
		{
			return true;
		}

		if (InspectRelicScreenRelicsField?.GetValue(__instance) is IReadOnlyList<RelicModel> relics
			&& InspectRelicScreenIndexField?.GetValue(__instance) is int index
			&& index >= 0
			&& index < relics.Count)
		{
			RelicModel relic = relics[index];
			if (HextechCatalog.IsHextechCustomRelic(relic))
			{
				RenderHextechInspect(__instance, relic);
				return false;
			}
		}

		return true;
	}

	private static void EnergyIconHelperGetPrefixPostfix(AbstractModel model, ref string __result)
	{
		if (model is RelicModel relic && HextechCatalog.IsHextechCustomRelic(relic))
		{
			__result = "red";
		}
	}

	private static void EnsureInspectRelicsUnlocked(NInspectRelicScreen screen, IReadOnlyList<RelicModel> relics)
	{
		if (InspectRelicScreenUnlockedRelicsField?.GetValue(screen) is not HashSet<RelicModel> unlockedRelics)
		{
			return;
		}

		foreach (RelicModel canonicalRelic in HextechCatalog.GetCanonicalVisibleCustomRelics())
		{
			unlockedRelics.Add(canonicalRelic);
		}

		foreach (RelicModel relic in relics)
		{
			if (!HextechCatalog.IsHextechCustomRelic(relic))
			{
				continue;
			}

			unlockedRelics.Add(EnsureCanonicalInstance(relic));
		}
	}

	private static RelicModel EnsureCanonicalInstance(RelicModel relic)
	{
		if (relic.CanonicalInstance != null)
		{
			return relic.CanonicalInstance;
		}

		RelicModel canonical = ModelDb.GetById<RelicModel>(relic.Id);
		RelicCanonicalInstanceField?.SetValue(relic, canonical);
		return canonical;
	}

	private static void RenderHextechInspect(NInspectRelicScreen screen, RelicModel relic)
	{
		if (InspectRelicScreenNameLabelField?.GetValue(screen) is not MegaLabel nameLabel
			|| InspectRelicScreenRarityLabelField?.GetValue(screen) is not MegaLabel rarityLabel
			|| InspectRelicScreenDescriptionField?.GetValue(screen) is not MegaRichTextLabel description
			|| InspectRelicScreenFlavorField?.GetValue(screen) is not MegaRichTextLabel flavor
			|| InspectRelicScreenImageField?.GetValue(screen) is not TextureRect image
			|| InspectRelicScreenHoverTipRectField?.GetValue(screen) is not Control hoverTipRect)
		{
			return;
		}

		nameLabel.SetTextAutoSize(relic.Title.GetFormattedText());
		LocString rarityText = new("gameplay_ui", "RELIC_RARITY." + relic.Rarity.ToString().ToUpperInvariant());
		rarityLabel.SetTextAutoSize(rarityText.GetFormattedText());
		image.SelfModulate = Colors.White;
		description.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
		flavor.SetTextAutoSize(relic.Flavor.GetFormattedText());
		InspectRelicScreenSetRarityVisualsMethod?.Invoke(screen, [relic.Rarity]);
		image.Texture = relic.BigIcon;

		NHoverTipSet.Clear();
		NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(screen, relic.HoverTipsExcludingRelic);
		hoverTipSet?.SetAlignment(hoverTipRect, HoverTip.GetHoverTipAlignment(screen));
	}

	private static bool HasInspectScreenMembers(out string missingMembers)
	{
		List<string> missing = [];
		AddMissing(InspectRelicScreenOpenMethod != null, "NInspectRelicScreen.Open");
		AddMissing(InspectRelicScreenUnlockedRelicsField != null, "NInspectRelicScreen._allUnlockedRelics");
		AddMissing(InspectRelicScreenRelicsField != null, "NInspectRelicScreen._relics");
		AddMissing(InspectRelicScreenIndexField != null, "NInspectRelicScreen._index");
		AddMissing(RelicCanonicalInstanceField != null, "RelicModel._canonicalInstance");
		AddMissing(InspectRelicScreenUpdateRelicDisplayMethod != null, "NInspectRelicScreen.UpdateRelicDisplay");
		AddMissing(InspectRelicScreenSetRelicMethod != null, "NInspectRelicScreen.SetRelic");
		AddMissing(InspectRelicScreenNameLabelField != null, "NInspectRelicScreen._nameLabel");
		AddMissing(InspectRelicScreenRarityLabelField != null, "NInspectRelicScreen._rarityLabel");
		AddMissing(InspectRelicScreenDescriptionField != null, "NInspectRelicScreen._description");
		AddMissing(InspectRelicScreenFlavorField != null, "NInspectRelicScreen._flavor");
		AddMissing(InspectRelicScreenImageField != null, "NInspectRelicScreen._relicImage");
		AddMissing(InspectRelicScreenHoverTipRectField != null, "NInspectRelicScreen._hoverTipRect");
		AddMissing(InspectRelicScreenSetRarityVisualsMethod != null, "NInspectRelicScreen.SetRarityVisuals");

		missingMembers = string.Join(", ", missing);
		return missing.Count == 0;

		void AddMissing(bool present, string memberName)
		{
			if (!present)
			{
				missing.Add(memberName);
			}
		}
	}

	private static void TryPatch(
		Harmony harmony,
		MethodBase? target,
		string label,
		string? prefix = null,
		string? postfix = null,
		int? prefixPriority = null,
		int? postfixPriority = null)
	{
		if (target == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Inspect hook skipped: missing {label}.");
			return;
		}

		try
		{
			harmony.Patch(
				target,
				GetHarmonyMethod(prefix, prefixPriority),
				GetHarmonyMethod(postfix, postfixPriority));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Inspect hook skipped: {label}: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static HarmonyMethod? GetHarmonyMethod(string? methodName, int? priority = null)
	{
		if (methodName == null)
		{
			return null;
		}

		var harmonyMethod = new HarmonyMethod(typeof(HextechInspectHooks), methodName);
		if (priority.HasValue)
		{
			harmonyMethod.priority = priority.Value;
		}

		return harmonyMethod;
	}

}
