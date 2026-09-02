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
	private static bool? _inspectScreenHooksAvailable;

	/// <summary>检视界面改写依赖 NInspectRelicScreen 的一批私有成员;任一缺失整体停用,只告警一次。</summary>
	private static bool InspectScreenHooksAvailable
	{
		get
		{
			if (_inspectScreenHooksAvailable is bool cached)
			{
				return cached;
			}

			if (!HasInspectScreenMembers(out string missingMembers))
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Inspect relic screen hooks disabled: missing {missingMembers}.");
				_inspectScreenHooksAvailable = false;
				return false;
			}

			_inspectScreenHooksInstalled = true;
			_inspectScreenHooksAvailable = true;
			return true;
		}
	}

	private readonly record struct InspectOpenState(RelicModel? RequestedRelic);


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


	[HarmonyPatch(typeof(UnlockState), nameof(UnlockState.Relics), MethodType.Getter)]
	[HextechPatch("ui.inspect.unlock-state-relics", "遗物检视界面", Optional = true)]
	private static class UnlockStateRelicsPatch
	{
		[HarmonyPostfix]
		private static void Postfix(ref IEnumerable<RelicModel> __result)
		{
			__result = __result.Concat(HextechCatalog.GetCanonicalVisibleCustomRelics()).Distinct();
		}
	}

	[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.IsRelicSeen), typeof(RelicModel))]
	[HextechPatch("ui.inspect.relic-seen", "遗物检视界面", Optional = true)]
	private static class IsRelicSeenPatch
	{
		[HarmonyPostfix]
		private static void Postfix(RelicModel relic, ref bool __result)
		{
			if (HextechCatalog.IsHextechCustomRelic(relic))
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPrefix), typeof(AbstractModel))]
	[HextechPatch("ui.inspect.energy-icon-prefix", "遗物检视界面", Optional = true)]
	private static class EnergyIconPrefixPatch
	{
		[HarmonyPostfix]
		private static void Postfix(AbstractModel model, ref string __result)
		{
			if (model is RelicModel relic && HextechCatalog.IsHextechCustomRelic(relic))
			{
				__result = "red";
			}
		}
	}

	[HarmonyPatch(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), typeof(IReadOnlyList<RelicModel>), typeof(RelicModel))]
	[HextechPatch("ui.inspect.open", "遗物检视界面", Optional = true)]
	private static class OpenPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => InspectScreenHooksAvailable;

		[HarmonyPrefix]
		[HarmonyPriority(Priority.Last)]
		private static void Prefix(ref IReadOnlyList<RelicModel> relics, ref RelicModel relic, out InspectOpenState __state)
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

		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(NInspectRelicScreen __instance, InspectOpenState __state)
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
	}

	[HarmonyPatch(typeof(NInspectRelicScreen), "UpdateRelicDisplay")]
	[HextechPatch("ui.inspect.update-display", "遗物检视界面", Optional = true)]
	private static class UpdateRelicDisplayPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => InspectScreenHooksAvailable;

		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(NInspectRelicScreen __instance)
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
	}
}
