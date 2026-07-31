using System.Text.Json;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

public sealed partial class SolidTimeRune
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private string? _lastLoggedCorruptStoredCardsJson;

	private void AppendStoredCard(CardModel deckCard)
	{
		List<StoredCard> cards = DecodeStoredCards();
		cards.Add(StoredCard.From(deckCard));
		_removedCardsJson = JsonSerializer.Serialize(cards, JsonOptions);
		_lastLoggedCorruptStoredCardsJson = null;
	}

	private List<StoredCard> DecodeStoredCards()
	{
		if (string.IsNullOrWhiteSpace(_removedCardsJson))
		{
			_lastLoggedCorruptStoredCardsJson = null;
			return [];
		}

		try
		{
			List<StoredCard> cards = (JsonSerializer.Deserialize<List<StoredCard>>(_removedCardsJson, JsonOptions) ?? [])
				.Where(IsStoredPowerCard)
				.ToList();
			_lastLoggedCorruptStoredCardsJson = null;
			return cards;
		}
		catch (Exception ex)
		{
			if (!string.Equals(_lastLoggedCorruptStoredCardsJson, _removedCardsJson, StringComparison.Ordinal))
			{
				_lastLoggedCorruptStoredCardsJson = _removedCardsJson;
				string preview = _removedCardsJson[..Math.Min(80, _removedCardsJson.Length)];
				Log.Warn(
					$"[{ModInfo.Id}][SolidTime] Stored cards JSON is corrupted; dropping {_removedCardsJson.Length} chars: "
					+ $"{ex.GetType().Name}: {ex.Message} json={preview}");
			}

			return [];
		}
	}

	private static bool IsStoredPowerCard(StoredCard stored)
	{
		CardModel? canonical = TryGetCanonical(stored);
		return IsStoredAsPowerCard(canonical, stored);
	}

	private static CardModel? CreatePreviewCard(StoredCard stored)
	{
		CardModel? canonical = TryGetCanonical(stored);
		if (canonical == null || !IsStoredAsPowerCard(canonical, stored))
		{
			return null;
		}

		CardModel preview = canonical.ToMutable();
		ApplyStoredCardState(preview, stored);
		ApplyUpgradeLevels(preview, stored.Upgrades);
		return preview;
	}

	private CardModel? CreateCombatCard(HextechCombatState combatState, StoredCard stored)
	{
		if (Owner == null)
		{
			return null;
		}

		CardModel? canonical = TryGetCanonical(stored);
		if (canonical == null || !IsStoredAsPowerCard(canonical, stored))
		{
			return null;
		}

		CardModel card = combatState.CreateCard(canonical, Owner);
		ApplyStoredCardState(card, stored);
		ApplyUpgradeLevels(card, stored.Upgrades);
		SaveManager.Instance.MarkCardAsSeen(card);
		return card;
	}

	private static bool IsStoredAsPowerCard(CardModel? canonical, StoredCard stored)
	{
		if (canonical is MadScience)
		{
			return stored.GetMadScienceCardType() == CardType.Power;
		}

		return canonical?.Type == CardType.Power;
	}

	private static CardModel? TryGetCanonical(StoredCard stored)
	{
		try
		{
			return ModelDb.GetById<CardModel>(new ModelId(stored.Category, stored.Entry));
		}
		catch
		{
			return null;
		}
	}

	private static void ApplyUpgradeLevels(CardModel card, int upgrades)
	{
		int count = Math.Clamp(upgrades, 0, card.MaxUpgradeLevel);
		for (int i = 0; i < count; i++)
		{
			card.UpgradeInternal();
			card.FinalizeUpgradeInternal();
		}
	}

	private static void ApplyStoredCardState(CardModel card, StoredCard stored)
	{
		if (card is MadScience madScience)
		{
			madScience.TinkerTimeType = stored.GetMadScienceCardType();
			madScience.TinkerTimeRider = stored.GetMadScienceRider();
		}
	}

	private sealed record StoredCard(
		string Category,
		string Entry,
		int Upgrades,
		int? MadScienceCardType = null,
		int? MadScienceRider = null)
	{
		public static StoredCard From(CardModel card)
		{
			ModelId id = card.CanonicalInstance.Id;
			if (card is MadScience madScience)
			{
				return new StoredCard(
					id.Category,
					id.Entry,
					card.CurrentUpgradeLevel,
					(int)madScience.TinkerTimeType,
					(int)madScience.TinkerTimeRider);
			}

			return new StoredCard(id.Category, id.Entry, card.CurrentUpgradeLevel);
		}

		public CardType GetMadScienceCardType()
		{
			return MadScienceCardType.HasValue
				? (CardType)MadScienceCardType.Value
				: CardType.Power;
		}

		public TinkerTime.RiderEffect GetMadScienceRider()
		{
			return MadScienceRider.HasValue
				? (TinkerTime.RiderEffect)MadScienceRider.Value
				: TinkerTime.RiderEffect.None;
		}
	}
}
