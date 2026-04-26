using System.Reflection;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using MonoMod.RuntimeDetour;

namespace HextechRunes;

internal static class HextechInspectHooks
{
	private static readonly FieldInfo InspectRelicScreenUnlockedRelicsField = RequireField(typeof(NInspectRelicScreen), "_allUnlockedRelics");
	private static readonly FieldInfo InspectRelicScreenRelicsField = RequireField(typeof(NInspectRelicScreen), "_relics");
	private static readonly FieldInfo InspectRelicScreenIndexField = RequireField(typeof(NInspectRelicScreen), "_index");
	private static readonly FieldInfo RelicCanonicalInstanceField = RequireField(typeof(RelicModel), "_canonicalInstance");
	private static readonly MethodInfo InspectRelicScreenUpdateRelicDisplayMethod = RequireMethod(typeof(NInspectRelicScreen), "UpdateRelicDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo InspectRelicScreenSetRelicMethod = RequireMethod(typeof(NInspectRelicScreen), "SetRelic", BindingFlags.Instance | BindingFlags.NonPublic, typeof(int));
	private static readonly FieldInfo InspectRelicScreenNameLabelField = RequireField(typeof(NInspectRelicScreen), "_nameLabel");
	private static readonly FieldInfo InspectRelicScreenRarityLabelField = RequireField(typeof(NInspectRelicScreen), "_rarityLabel");
	private static readonly FieldInfo InspectRelicScreenDescriptionField = RequireField(typeof(NInspectRelicScreen), "_description");
	private static readonly FieldInfo InspectRelicScreenFlavorField = RequireField(typeof(NInspectRelicScreen), "_flavor");
	private static readonly FieldInfo InspectRelicScreenImageField = RequireField(typeof(NInspectRelicScreen), "_relicImage");
	private static readonly FieldInfo InspectRelicScreenHoverTipRectField = RequireField(typeof(NInspectRelicScreen), "_hoverTipRect");
	private static readonly MethodInfo InspectRelicScreenSetRarityVisualsMethod = RequireMethod(typeof(NInspectRelicScreen), "SetRarityVisuals", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicRarity));

	private static Hook? _unlockStateRelicsHook;
	private static Hook? _saveManagerIsRelicSeenHook;
	private static Hook? _inspectRelicScreenOpenHook;
	private static Hook? _inspectRelicScreenUpdateRelicDisplayHook;
	private static Hook? _energyIconPrefixHook;

	private delegate IEnumerable<RelicModel> OrigGetUnlockStateRelics(UnlockState self);

	private delegate bool OrigIsRelicSeen(SaveManager self, RelicModel relic);

	private delegate void OrigInspectRelicScreenOpen(NInspectRelicScreen self, IReadOnlyList<RelicModel> relics, RelicModel relic);

	private delegate void OrigInspectRelicScreenUpdateRelicDisplay(NInspectRelicScreen self);

	private delegate string OrigEnergyIconHelperGetPrefix(AbstractModel model);

	public static void Install()
	{
		_unlockStateRelicsHook = new Hook(
			typeof(UnlockState).GetProperty(nameof(UnlockState.Relics), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetMethod!,
			GetUnlockStateRelicsDetour);
		_saveManagerIsRelicSeenHook = new Hook(
			RequireMethod(typeof(SaveManager), nameof(SaveManager.IsRelicSeen), BindingFlags.Instance | BindingFlags.Public, typeof(RelicModel)),
			IsRelicSeenDetour);
		_inspectRelicScreenOpenHook = new Hook(
			RequireMethod(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), BindingFlags.Instance | BindingFlags.Public, typeof(IReadOnlyList<RelicModel>), typeof(RelicModel)),
			InspectRelicScreenOpenDetour);
		_inspectRelicScreenUpdateRelicDisplayHook = new Hook(
			InspectRelicScreenUpdateRelicDisplayMethod,
			InspectRelicScreenUpdateRelicDisplayDetour);
		_energyIconPrefixHook = new Hook(
			RequireMethod(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPrefix), BindingFlags.Static | BindingFlags.Public, typeof(AbstractModel)),
			EnergyIconHelperGetPrefixDetour);
	}

	private static IEnumerable<RelicModel> GetUnlockStateRelicsDetour(OrigGetUnlockStateRelics orig, UnlockState self)
	{
		return orig(self).Concat(ModInfo.GetCanonicalCustomRelics()).Distinct();
	}

	private static bool IsRelicSeenDetour(OrigIsRelicSeen orig, SaveManager self, RelicModel relic)
	{
		if (ModInfo.IsHextechCustomRelic(relic))
		{
			return true;
		}

		return orig(self, relic);
	}

	private static void InspectRelicScreenOpenDetour(OrigInspectRelicScreenOpen orig, NInspectRelicScreen self, IReadOnlyList<RelicModel> relics, RelicModel relic)
	{
		List<RelicModel> correctedRelics = relics.ToList();
		int correctedIndex = correctedRelics.FindIndex(candidate => ReferenceEquals(candidate, relic) || candidate.Id == relic.Id);
		if (correctedIndex < 0)
		{
			correctedRelics.Add(relic);
			correctedIndex = correctedRelics.Count - 1;
		}

		orig(self, correctedRelics, correctedRelics[correctedIndex]);
		EnsureInspectRelicsUnlocked(self, correctedRelics);
		InspectRelicScreenRelicsField.SetValue(self, correctedRelics);
		InspectRelicScreenSetRelicMethod.Invoke(self, [correctedIndex]);
		InspectRelicScreenUpdateRelicDisplayMethod.Invoke(self, null);
	}

	private static void InspectRelicScreenUpdateRelicDisplayDetour(OrigInspectRelicScreenUpdateRelicDisplay orig, NInspectRelicScreen self)
	{
		if (InspectRelicScreenRelicsField.GetValue(self) is IReadOnlyList<RelicModel> relics
			&& InspectRelicScreenIndexField.GetValue(self) is int index
			&& index >= 0
			&& index < relics.Count)
		{
			RelicModel relic = relics[index];
			if (ModInfo.IsHextechCustomRelic(relic))
			{
				RenderHextechInspect(self, relic);
				return;
			}
		}

		orig(self);
	}

	private static string EnergyIconHelperGetPrefixDetour(OrigEnergyIconHelperGetPrefix orig, AbstractModel model)
	{
		if (model is RelicModel relic && ModInfo.IsHextechCustomRelic(relic))
		{
			return "red";
		}

		return orig(model);
	}

	private static void EnsureInspectRelicsUnlocked(NInspectRelicScreen screen, IReadOnlyList<RelicModel> relics)
	{
		if (InspectRelicScreenUnlockedRelicsField.GetValue(screen) is not HashSet<RelicModel> unlockedRelics)
		{
			return;
		}

		foreach (RelicModel canonicalRelic in ModInfo.GetCanonicalCustomRelics())
		{
			unlockedRelics.Add(canonicalRelic);
		}

		foreach (RelicModel relic in relics)
		{
			if (!ModInfo.IsHextechCustomRelic(relic))
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
		RelicCanonicalInstanceField.SetValue(relic, canonical);
		return canonical;
	}

	private static void RenderHextechInspect(NInspectRelicScreen screen, RelicModel relic)
	{
		MegaLabel nameLabel = (MegaLabel)InspectRelicScreenNameLabelField.GetValue(screen)!;
		MegaLabel rarityLabel = (MegaLabel)InspectRelicScreenRarityLabelField.GetValue(screen)!;
		MegaRichTextLabel description = (MegaRichTextLabel)InspectRelicScreenDescriptionField.GetValue(screen)!;
		MegaRichTextLabel flavor = (MegaRichTextLabel)InspectRelicScreenFlavorField.GetValue(screen)!;
		TextureRect image = (TextureRect)InspectRelicScreenImageField.GetValue(screen)!;
		Control hoverTipRect = (Control)InspectRelicScreenHoverTipRectField.GetValue(screen)!;

		nameLabel.SetTextAutoSize(relic.Title.GetFormattedText());
		LocString rarityText = new("gameplay_ui", "RELIC_RARITY." + relic.Rarity.ToString().ToUpperInvariant());
		rarityLabel.SetTextAutoSize(rarityText.GetFormattedText());
		image.SelfModulate = Colors.White;
		description.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
		flavor.SetTextAutoSize(relic.Flavor.GetFormattedText());
		InspectRelicScreenSetRarityVisualsMethod.Invoke(screen, [relic.Rarity]);
		image.Texture = relic.BigIcon;

		NHoverTipSet.Clear();
		NHoverTipSet hoverTipSet = NHoverTipSet.CreateAndShow(screen, relic.HoverTipsExcludingRelic);
		hoverTipSet.SetAlignment(hoverTipRect, HoverTip.GetHoverTipAlignment(screen));
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
	}
}
