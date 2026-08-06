namespace HextechRunes;

public sealed class UndyingUpgradeRune : CardUpgradeRuneBase<Undeath>
{
	internal const string EtherealMarkerSavedPropertyName = "SavedUndyingUpgradeEtherealMarker";

	public override bool HasUponPickupEffect => true;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedUndyingUpgradeEtherealMarker
	{
		get => 0;
		set { }
	}

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Undeath>(),
		HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterObtained()
	{
		// 先加入不死，再给全部不死（含刚加入的牌）加持久虚无词条。
		await base.AfterObtained();
		if (Owner == null)
		{
			return;
		}

		List<CardModel> undeaths = Owner.Deck.Cards
			.Where(static card => card is Undeath)
			.ToList();
		if (undeaths.Count == 0)
		{
			return;
		}

		Flash();
		foreach (CardModel card in undeaths)
		{
			ApplyPersistentEthereal(card);
		}
	}

	public override Task AfterCardEnteredCombat(CardModel card)
	{
		if (Owner == null || card.Owner != Owner)
		{
			return Task.CompletedTask;
		}

		if (UndyingEtherealKeywordPersistence.IsTracked(card.DeckVersion))
		{
			UndyingEtherealKeywordPersistence.Restore(card);
		}

		return Task.CompletedTask;
	}

	public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
	{
		newCard = null;
		if (card.Owner != Owner || card is not Undeath)
		{
			return false;
		}

		ApplyPersistentEthereal(card);
		newCard = card;
		Flash();
		return true;
	}

	private static void ApplyPersistentEthereal(CardModel card)
	{
		UndyingEtherealKeywordPersistence.Track(card);
		CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
	}
}
