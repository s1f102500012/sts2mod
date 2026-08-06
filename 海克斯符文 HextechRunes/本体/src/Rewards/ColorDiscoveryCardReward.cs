using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal sealed class ColorDiscoveryCardReward : CardReward
{
	private static readonly FieldInfo? SpecialCardRewardCardField = TryGetField(typeof(SpecialCardReward), "_card");

	private readonly ModelId _cardId;
	private readonly CardCreationSource _source;
	private readonly CardRarityOddsType _rarityOdds;

	public ColorDiscoveryCardReward(
		ModelId cardId,
		Player player,
		CardCreationSource source = CardCreationSource.Encounter,
		CardRarityOddsType rarityOdds = CardRarityOddsType.Uniform)
		: base(CreateCardsToOffer(cardId, player), source, player, CreateRerollOptions(cardId, source, rarityOdds))
	{
		_cardId = cardId;
		_source = source;
		_rarityOdds = rarityOdds;
		CanReroll = false;
	}

	private ColorDiscoveryCardReward(
		CardModel card,
		ModelId cardId,
		Player player,
		CardCreationSource source,
		CardRarityOddsType rarityOdds)
		: base([card], source, player, CreateRerollOptions(cardId, source, rarityOdds))
	{
		_cardId = cardId;
		_source = source;
		_rarityOdds = rarityOdds;
		CanReroll = false;
	}

	public static ColorDiscoveryCardReward FromSavedReward(SerializableReward save, Player player)
	{
		CardCreationSource source = save.Source;
		CardRarityOddsType rarityOdds = save.RarityOdds;
		return new ColorDiscoveryCardReward(save.PredeterminedModelId, player, source, rarityOdds);
	}

	internal static bool TryFromSavedSpecialCardReward(
		SerializableReward save,
		Reward? restoredReward,
		Player player,
		out ColorDiscoveryCardReward? reward,
		bool logFailure = true)
	{
		reward = null;
		CardModel? card = TryGetRestoredSpecialCard(restoredReward, SpecialCardRewardCardField);
		if (card == null)
		{
			if (logFailure
				&& restoredReward != null
				&& HextechRunLogBudget.TryConsume("rewards.color-discovery-special-card-restore", 1))
			{
				Log.Warn(
					$"[{ModInfo.Id}][Rewards] Color Discovery reward kept as the original SpecialCardReward because its restored card could not be read; "
					+ $"rewardType={restoredReward.GetType().FullName} fieldAvailable={SpecialCardRewardCardField != null}.");
			}

			return false;
		}

		ModelId cardId = card.CanonicalInstance?.Id ?? card.Id;
		reward = new ColorDiscoveryCardReward(card, cardId, player, save.Source, save.RarityOdds);
		return true;
	}

	public override SerializableReward ToSerializable()
	{
		CardModel card = GetCurrentRewardCard() ?? ModelDb.GetById<CardModel>(_cardId);
		return new SerializableReward
		{
			RewardType = RewardType.SpecialCard,
			Source = _source,
			RarityOdds = _rarityOdds,
			OptionCount = 1,
			SpecialCard = card.ToSerializable(),
			PredeterminedModelId = ModelDb.GetId<ColorDiscoveryRune>(),
		};
	}

	private static IEnumerable<CardModel> CreateCardsToOffer(ModelId cardId, Player player)
	{
		CardModel canonicalCard = ModelDb.GetById<CardModel>(cardId);
		yield return player.RunState.CreateCard(canonicalCard, player);
	}

	private static CardCreationOptions CreateRerollOptions(
		ModelId cardId,
		CardCreationSource source,
		CardRarityOddsType rarityOdds)
	{
		CardModel canonicalCard = ModelDb.GetById<CardModel>(cardId);
#if STS2_108_OR_NEWER
		// 0.108.0 无自定义卡列表构造:用该卡所属池+按 Id 过滤等价表达"只出这张卡"。
		CardCreationOptions options = new([canonicalCard.Pool], source, rarityOdds, card => card.Id.Equals(cardId));
#else
		CardCreationOptions options = new([canonicalCard], source, rarityOdds);
#endif
#if STS2_105_OR_NEWER
		options.WithFlags(CardCreationFlags.IsCardReward);
#endif
		return options;
	}

	private CardModel? GetCurrentRewardCard()
	{
		return GetFirstOfferedCard(Cards);
	}

	internal static CardModel? GetFirstOfferedCard(IEnumerable<CardModel> cards)
	{
		return cards.FirstOrDefault();
	}

	internal static CardModel? TryGetRestoredSpecialCard(object? restoredReward, FieldInfo? cardField)
	{
		if (restoredReward == null || cardField == null)
		{
			return null;
		}

		try
		{
			return cardField.GetValue(restoredReward) as CardModel;
		}
		catch (Exception ex)
		{
			if (HextechRunLogBudget.TryConsume("rewards.color-discovery-special-card-field-read", 1))
			{
				Log.Warn(
					$"[{ModInfo.Id}][Rewards] SpecialCardReward card field read failed; keeping the original reward: "
					+ $"rewardType={restoredReward.GetType().FullName} error={ex.GetType().Name}: {ex.Message}");
			}

			return null;
		}
	}
}
