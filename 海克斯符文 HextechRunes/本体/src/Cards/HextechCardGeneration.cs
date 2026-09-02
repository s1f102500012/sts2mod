using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static class HextechCardGeneration
{
	internal static async Task<IReadOnlyList<CardPileAddResult>> AddGeneratedCardsToCombat(
		IEnumerable<CardModel> cards,
		PileType pileType,
		bool addedByPlayer,
		CardPilePosition position = CardPilePosition.Bottom,
		bool previewNonHandAdds = true)
	{
		List<CardModel> cardList = cards.ToList();
		if (cardList.Count == 0)
		{
			return Array.Empty<CardPileAddResult>();
		}

		Player? creator = addedByPlayer ? cardList.FirstOrDefault()?.Owner : null;
		IReadOnlyList<CardPileAddResult> results = await CardPileCmd.AddGeneratedCardsToCombat(
			cardList,
			pileType,
			creator,
			position);
		foreach (CardModel card in cardList)
		{
			SaveManager.Instance.MarkCardAsSeen(card);
		}

		if (previewNonHandAdds && pileType != PileType.Hand && results.Count > 0)
		{
			CardCmd.PreviewCardPileAdd(results);
		}

		return results;
	}

	internal static async Task<CardPileAddResult?> AddGeneratedCardToCombat(
		CardModel card,
		PileType pileType,
		bool addedByPlayer,
		CardPilePosition position = CardPilePosition.Bottom,
		bool previewNonHandAdds = true)
	{
		IReadOnlyList<CardPileAddResult> results = await AddGeneratedCardsToCombat(
			[card],
			pileType,
			addedByPlayer,
			position,
			previewNonHandAdds);
		return results.Count > 0 ? results[0] : null;
	}
}
