using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents.Relics;

public sealed class BrokenSwordRelic : IntegratedStrategyEventRelic
{
	private const decimal EnergyReduction = 1m;
	private const decimal MinimumAffectedCost = 2m;

	public BrokenSwordRelic()
		: base("broken_sword.png")
	{
	}

	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldAffect(card))
		{
			return false;
		}

		modifiedCost = Math.Max(0m, originalCost - EnergyReduction);
		return true;
	}

	public override CardLocation ModifyCardPlayResultLocation(
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		CardLocation cardLocation)
	{
		if (!ShouldAffect(card) || cardLocation.pileType == PileType.None)
		{
			return cardLocation;
		}

		Flash();
		return cardLocation with { pileType = PileType.Exhaust };
	}

	private bool ShouldAffect(CardModel card)
	{
		// CostModifiers.None = 含升级的印刷费用（与剑锤同步：按升级后的实际印刷费判定门槛）。
		return IsOwnedCard(card)
			&& IsNonXEnergyCard(card)
			&& card.EnergyCost.GetWithModifiers(CostModifiers.None) >= MinimumAffectedCost;
	}
}
