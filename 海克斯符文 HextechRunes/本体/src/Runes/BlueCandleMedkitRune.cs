namespace HextechRunes;

public sealed class BlueCandleMedkitRune : HextechRelicBase
{
	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!CanAffect(card) || card.EnergyCost.CostsX)
		{
			return false;
		}

		modifiedCost = 0m;
		return true;
	}

	public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!CanAffect(card) || card.HasStarCostX)
		{
			return false;
		}

		modifiedCost = 0m;
		return true;
	}

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		// 去向 None(复制品/能力牌)不抢改:改成消耗堆会留下滞留的幽灵实体(空白手牌位问题同源)。
		return pileType is not PileType.None && CanAffect(card) ? (PileType.Exhaust, position) : (pileType, position);
	}

	internal static bool AllowsPlaying(CardModel card)
	{
		return card.Owner?.GetRelic<BlueCandleMedkitRune>() is BlueCandleMedkitRune rune
			&& rune.CanAffect(card);
	}

	// 状态/诅咒牌在手牌里变得可打出:直接从战斗内关键词集合摘掉 Unplayable,
	// CanPlay 的 HasUnplayableKeyword 判定与卡面上的"无法打出"文字随之一起消失。
	public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
	{
		return CanAffect(card) && keywords.Remove(CardKeyword.Unplayable);
	}

	private bool CanAffect(CardModel card)
	{
		return Owner != null
			&& card.Owner == Owner
			&& card.Pile?.Type is PileType.Hand or PileType.Play
			&& card.Type is CardType.Status or CardType.Curse;
	}
}
