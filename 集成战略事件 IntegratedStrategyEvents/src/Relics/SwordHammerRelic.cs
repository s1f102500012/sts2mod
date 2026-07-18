using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents.Relics;

public sealed class SwordHammerRelic : IntegratedStrategyEventRelic
{
	private const decimal EnergyIncrease = 1m;
	private const int AdditionalPlayCount = 1;

	public SwordHammerRelic()
		: base("sword_hammer.png")
	{
	}

	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldAffect(card))
		{
			return false;
		}

		modifiedCost = originalCost + EnergyIncrease;
		return true;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (!ShouldAffect(card))
		{
			return playCount;
		}

		Flash();
		return playCount + AdditionalPlayCount;
	}

	private bool ShouldAffect(CardModel card)
	{
		// CostModifiers.None = 含升级的印刷费用（Canonical 不含升级，会漏掉升级后才 0 费的牌）。
		return IsOwnedCard(card)
			&& IsNonXEnergyCard(card)
			&& card.EnergyCost.GetWithModifiers(CostModifiers.None) == 0;
	}
}
