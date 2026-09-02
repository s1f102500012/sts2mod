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

	// 原版克隆(DeepCloneFields)只按"有来源"的关键词重建 _keywords,思维覆写/谢幕/扮演/腐化枝/不死这类
	// 运行期附加的关键词与追踪标记都会丢;镜中倒影、复视等复制整副牌组的路径拿到的副本因此没有虚无词条
	// (玩家反馈)。牌组级克隆把源牌的持久化快照原样恢复到副本上,副本自己成为被追踪的牌组版本。
	[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Runs.RunState), nameof(MegaCrit.Sts2.Core.Runs.RunState.CloneCard), typeof(CardModel))]
	[HextechPatch("card.keyword-persistence.clone-deck", "关键词持久化")]
	private static class RunStateCloneCardPatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel mutableCard, CardModel __result)
		{
			KeywordPersistenceSnapshot.Capture(mutableCard).Restore(__result);
		}
	}

	[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Combat.CombatState), nameof(MegaCrit.Sts2.Core.Combat.CombatState.CloneCard), typeof(CardModel))]
	[HextechPatch("card.keyword-persistence.clone-combat", "关键词持久化")]
	private static class CombatStateCloneCardPatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel mutableCard, CardModel __result)
		{
			KeywordPersistenceSnapshot.Capture(mutableCard).Restore(__result);
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.ToSerializable), new Type[0])]
	[HextechPatch("card.keyword-persistence.save", "关键词持久化")]
	private static class ToSerializablePatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, SerializableCard __result)
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
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable), typeof(SerializableCard))]
	[HextechPatch("card.keyword-persistence.load", "关键词持久化")]
	private static class FromSerializablePatch
	{
		[HarmonyPostfix]
		private static void Postfix(SerializableCard save, CardModel __result)
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
	}

	[HarmonyPatch]
	[HextechPatch("card.keyword-persistence.rebuild", "关键词持久化")]
	private static class KeywordRebuildPatch
	{
		[HarmonyTargetMethods]
		private static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.DowngradeInternal), Type.EmptyTypes);
			yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.FinalizeUpgradeInternal), Type.EmptyTypes);
		}

		[HarmonyPrefix]
		private static void Prefix(CardModel __instance, out KeywordPersistenceSnapshot __state)
		{
			__state = KeywordPersistenceSnapshot.Capture(__instance);
		}

		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, KeywordPersistenceSnapshot __state)
		{
			__state.Restore(__instance);
		}
	}
}
