using System.Runtime.CompilerServices;
using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class ThoughtOverwriteKeywordPersistence
{
	private static readonly ConditionalWeakTable<CardModel, Marker> TrackedCards = new();

	private sealed class Marker
	{
	}

	public static void Track(CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		TrackedCards.GetValue(card, static _ => new Marker());
	}

	public static bool IsTracked(CardModel? card)
	{
		return card != null && TrackedCards.TryGetValue(card, out _);
	}

	public static void Restore(CardModel card)
	{
		Track(card);
		if (!card.Keywords.Contains(CardKeyword.Ethereal))
		{
			card.AddKeyword(CardKeyword.Ethereal);
		}
	}

	public static bool ShouldPersist(CardModel card)
	{
		return IsTracked(card) || IsTracked(card.DeckVersion);
	}
}

internal static class CurtainCallKeywordPersistence
{
	private static readonly ConditionalWeakTable<CardModel, Marker> TrackedCards = new();

	private sealed class Marker
	{
	}

	public static void Track(CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		TrackedCards.GetValue(card, static _ => new Marker());
	}

	public static bool IsTracked(CardModel? card)
	{
		return card != null && TrackedCards.TryGetValue(card, out _);
	}

	public static void Restore(CardModel card)
	{
		Track(card);
		if (!card.Keywords.Contains(CardKeyword.Retain))
		{
			card.AddKeyword(CardKeyword.Retain);
		}
	}

	public static bool ShouldPersist(CardModel card)
	{
		return IsTracked(card) || IsTracked(card.DeckVersion);
	}
}

internal static class CosplayInnateKeywordPersistence
{
	private static readonly ConditionalWeakTable<CardModel, Marker> TrackedCards = new();

	private sealed class Marker
	{
	}

	public static void Track(CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		TrackedCards.GetValue(card, static _ => new Marker());
	}

	public static bool IsTracked(CardModel? card)
	{
		return card != null && TrackedCards.TryGetValue(card, out _);
	}

	public static void Restore(CardModel card)
	{
		Track(card);
		if (!card.Keywords.Contains(CardKeyword.Innate))
		{
			card.AddKeyword(CardKeyword.Innate);
		}
	}

	public static bool ShouldPersist(CardModel card)
	{
		return IsTracked(card) || IsTracked(card.DeckVersion);
	}
}

internal static class CorruptedBranchInnateKeywordPersistence
{
	private static readonly ConditionalWeakTable<CardModel, Marker> TrackedCards = new();

	private sealed class Marker
	{
	}

	public static void Track(CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		TrackedCards.GetValue(card, static _ => new Marker());
	}

	public static bool IsTracked(CardModel? card)
	{
		return card != null && TrackedCards.TryGetValue(card, out _);
	}

	public static void Restore(CardModel card)
	{
		Track(card);
		if (!card.Keywords.Contains(CardKeyword.Innate))
		{
			card.AddKeyword(CardKeyword.Innate);
		}
	}

	public static bool ShouldPersist(CardModel card)
	{
		return IsTracked(card) || IsTracked(card.DeckVersion);
	}
}

internal static class UndyingEtherealKeywordPersistence
{
	private static readonly ConditionalWeakTable<CardModel, Marker> TrackedCards = new();

	private sealed class Marker
	{
	}

	public static void Track(CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		TrackedCards.GetValue(card, static _ => new Marker());
	}

	public static bool IsTracked(CardModel? card)
	{
		return card != null && TrackedCards.TryGetValue(card, out _);
	}

	public static void Restore(CardModel card)
	{
		Track(card);
		if (!card.Keywords.Contains(CardKeyword.Ethereal))
		{
			card.AddKeyword(CardKeyword.Ethereal);
		}
	}

	public static bool ShouldPersist(CardModel card)
	{
		return IsTracked(card) || IsTracked(card.DeckVersion);
	}
}

internal static class HextechThoughtOverwriteKeywordPersistenceHooks
{
	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.ToSerializable), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechThoughtOverwriteKeywordPersistenceHooks), nameof(CardToSerializablePostfix)));
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.FromSerializable), BindingFlags.Static | BindingFlags.Public, typeof(SerializableCard)),
			postfix: new HarmonyMethod(typeof(HextechThoughtOverwriteKeywordPersistenceHooks), nameof(CardFromSerializablePostfix)));
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.DowngradeInternal), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(HextechThoughtOverwriteKeywordPersistenceHooks), nameof(CardKeywordRebuildPrefix)),
			postfix: new HarmonyMethod(typeof(HextechThoughtOverwriteKeywordPersistenceHooks), nameof(CardKeywordRebuildPostfix)));
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.FinalizeUpgradeInternal), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(HextechThoughtOverwriteKeywordPersistenceHooks), nameof(CardKeywordRebuildPrefix)),
			postfix: new HarmonyMethod(typeof(HextechThoughtOverwriteKeywordPersistenceHooks), nameof(CardKeywordRebuildPostfix)));
	}

	private readonly struct KeywordPersistenceSnapshot
	{
		private readonly bool _thoughtOverwrite;
		private readonly bool _curtainCall;
		private readonly bool _cosplayInnate;
		private readonly bool _corruptedBranchInnate;
		private readonly bool _undyingEthereal;

		private KeywordPersistenceSnapshot(
			bool thoughtOverwrite,
			bool curtainCall,
			bool cosplayInnate,
			bool corruptedBranchInnate,
			bool undyingEthereal)
		{
			_thoughtOverwrite = thoughtOverwrite;
			_curtainCall = curtainCall;
			_cosplayInnate = cosplayInnate;
			_corruptedBranchInnate = corruptedBranchInnate;
			_undyingEthereal = undyingEthereal;
		}

		public static KeywordPersistenceSnapshot Capture(CardModel? card)
		{
			if (card == null)
			{
				return default;
			}

			return new KeywordPersistenceSnapshot(
				ThoughtOverwriteKeywordPersistence.ShouldPersist(card),
				CurtainCallKeywordPersistence.ShouldPersist(card),
				CosplayInnateKeywordPersistence.ShouldPersist(card),
				CorruptedBranchInnateKeywordPersistence.ShouldPersist(card),
				UndyingEtherealKeywordPersistence.ShouldPersist(card));
		}

		public void Restore(CardModel? card)
		{
			if (card == null)
			{
				return;
			}

			if (_thoughtOverwrite)
			{
				ThoughtOverwriteKeywordPersistence.Restore(card);
			}

			if (_curtainCall)
			{
				CurtainCallKeywordPersistence.Restore(card);
			}

			if (_cosplayInnate)
			{
				CosplayInnateKeywordPersistence.Restore(card);
			}

			if (_corruptedBranchInnate)
			{
				CorruptedBranchInnateKeywordPersistence.Restore(card);
			}

			if (_undyingEthereal)
			{
				UndyingEtherealKeywordPersistence.Restore(card);
			}
		}
	}

	private static void CardKeywordRebuildPrefix(CardModel __instance, out KeywordPersistenceSnapshot __state)
	{
		__state = KeywordPersistenceSnapshot.Capture(__instance);
	}

	private static void CardKeywordRebuildPostfix(CardModel __instance, KeywordPersistenceSnapshot __state)
	{
		__state.Restore(__instance);
	}

	private static void CardToSerializablePostfix(CardModel __instance, SerializableCard __result)
	{
		if (ThoughtOverwriteKeywordPersistence.ShouldPersist(__instance))
		{
			AddMarker(__result, ThoughtOverwriteRune.EtherealMarkerSavedPropertyName);
		}

		if (CurtainCallKeywordPersistence.ShouldPersist(__instance))
		{
			AddMarker(__result, CurtainCallRune.RetainMarkerSavedPropertyName);
		}

		if (CosplayInnateKeywordPersistence.ShouldPersist(__instance))
		{
			AddMarker(__result, HextechRunesApi.PersistentInnateMarkerSavedPropertyName);
		}

		if (CorruptedBranchInnateKeywordPersistence.ShouldPersist(__instance))
		{
			AddMarker(__result, CorruptedBranchRune.InnateMarkerSavedPropertyName);
		}

		if (UndyingEtherealKeywordPersistence.ShouldPersist(__instance))
		{
			AddMarker(__result, UndyingUpgradeRune.EtherealMarkerSavedPropertyName);
		}
	}

	private static void CardFromSerializablePostfix(SerializableCard save, CardModel __result)
	{
		if (HasMarker(save.Props, ThoughtOverwriteRune.EtherealMarkerSavedPropertyName))
		{
			ThoughtOverwriteKeywordPersistence.Restore(__result);
		}

		if (HasMarker(save.Props, CurtainCallRune.RetainMarkerSavedPropertyName))
		{
			CurtainCallKeywordPersistence.Restore(__result);
		}

		if (HasMarker(save.Props, HextechRunesApi.PersistentInnateMarkerSavedPropertyName))
		{
			CosplayInnateKeywordPersistence.Restore(__result);
		}

		if (HasMarker(save.Props, CorruptedBranchRune.InnateMarkerSavedPropertyName))
		{
			CorruptedBranchInnateKeywordPersistence.Restore(__result);
		}

		if (HasMarker(save.Props, UndyingUpgradeRune.EtherealMarkerSavedPropertyName))
		{
			UndyingEtherealKeywordPersistence.Restore(__result);
		}
	}

	private static void AddMarker(SerializableCard card, string markerSavedPropertyName)
	{
		card.Props ??= new SavedProperties();
		card.Props.ints ??= new List<SavedProperties.SavedProperty<int>>();
		if (card.Props.ints.Any(property => property.name == markerSavedPropertyName))
		{
			return;
		}

		card.Props.ints.Add(new SavedProperties.SavedProperty<int>(
			markerSavedPropertyName,
			1));
	}

	private static bool HasMarker(SavedProperties? props, string markerSavedPropertyName)
	{
		return props?.ints?.Any(property =>
			property.name == markerSavedPropertyName
			&& property.value != 0) == true;
	}
}
