using Godot;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace HextechRunes;

internal static partial class HextechCollectionHooks
{
	private static void AddFlatFallbackRelics(NRelicCollectionCategory self, NRelicCollection collection)
	{
		if (collection.Relics.Any(IsHextechCollectionRelic))
		{
			return;
		}

		if (RelicsContainerField?.GetValue(self) is not GridContainer relicsContainer)
		{
			if (!_loggedMissingFallbackContainer)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Relic collection flat fallback skipped: starter relic grid is unavailable.");
				_loggedMissingFallbackContainer = true;
			}

			return;
		}

		List<RelicModel> relics = GetFlatFallbackRelics();
		if (relics.Count == 0)
		{
			return;
		}

		collection.AddRelics(relics);
		foreach (RelicModel relic in relics)
		{
			NRelicCollectionEntry entry = NRelicCollectionEntry.Create(relic, ModelVisibility.Visible);
			relicsContainer.AddChild(entry);
			entry.Connect(
				NClickableControl.SignalName.Released,
				Callable.From<NRelicCollectionEntry>(entry => OpenFallbackRelic(collection, entry)));
		}

		if (!_loggedFlatFallback)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Added {relics.Count} Hextech relics to the starter relic collection grid through the mobile fallback.");
			_loggedFlatFallback = true;
		}
	}

	private static List<RelicModel> GetFlatFallbackRelics()
	{
		return HextechCatalog.GetCanonicalGenericVisibleRunes()
			.Concat(HextechCatalog.GetCharacterRuneGroups().SelectMany(static group => group.Relics))
			.Concat(HextechCatalog.GetCanonicalForges())
			.Distinct()
			.ToList();
	}

	private static bool IsHextechCollectionRelic(RelicModel relic)
	{
		return HextechCatalog.IsHextechRelic(relic) || HextechCatalog.IsHextechForgeRelic(relic);
	}

	private static void OpenFallbackRelic(NRelicCollection collection, NRelicCollectionEntry entry)
	{
		if (NGame.Instance == null)
		{
			return;
		}

		NGame.Instance.GetInspectRelicScreen().Open(collection.Relics, entry.relic);
		collection.SetLastFocusedRelic(entry);
	}
}
