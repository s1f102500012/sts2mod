using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

public sealed class Evolution : EnchantmentModel
{
	private const decimal StatGrowth = 2m;
	private const int CostReduction = 1;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int EvolutionPlayCount { get; set; }

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("StatGrowth", StatGrowth),
		new EnergyVar("CostReduction", CostReduction)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
	];

	protected override void OnEnchant()
	{
		Card.AddKeyword(CardKeyword.Exhaust);
		ApplyEvolutionGrowth(Card, EvolutionPlayCount, markUpgraded: false);
	}

	public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Card != Card)
		{
			return Task.CompletedTask;
		}

		ApplyOneEvolutionGrowth(Card, this);
		if (Card.DeckVersion is { } deckCard
			&& !ReferenceEquals(deckCard, Card)
			&& deckCard.Enchantment is Evolution deckEvolution)
		{
			ApplyOneEvolutionGrowth(deckCard, deckEvolution);
		}

		return Task.CompletedTask;
	}

	private static void ApplyOneEvolutionGrowth(CardModel card, Evolution evolution)
	{
		evolution.EvolutionPlayCount++;
		ApplyEvolutionGrowth(card, 1, markUpgraded: true);
	}

	private static void ApplyEvolutionGrowth(CardModel card, int playCount, bool markUpgraded)
	{
		if (playCount <= 0)
		{
			return;
		}

		switch (card.Type)
		{
			case CardType.Attack:
				UpgradeVars<DamageVar>(card, StatGrowth * playCount, markUpgraded);
				break;
			case CardType.Skill:
				UpgradeVars<BlockVar>(card, StatGrowth * playCount, markUpgraded);
				break;
			case CardType.Power:
				ReduceEnergyCost(card, CostReduction * playCount);
				break;
		}
	}

	private static void UpgradeVars<TVar>(CardModel card, decimal amount, bool markUpgraded)
		where TVar : DynamicVar
	{
		bool changed = false;
		foreach (TVar dynamicVar in card.DynamicVars.Values.OfType<TVar>())
		{
			if (markUpgraded)
			{
				dynamicVar.UpgradeValueBy(amount);
			}
			else
			{
				dynamicVar.BaseValue += amount;
			}
			changed = true;
		}

		if (changed)
		{
			card.DynamicVars.RecalculateForUpgradeOrEnchant();
			card.AfterForged();
		}
	}

	private static void ReduceEnergyCost(CardModel card, int amount)
	{
		if (amount <= 0 || card.EnergyCost.CostsX)
		{
			return;
		}

		card.EnergyCost.UpgradeBy(-amount);
		card.FinalizeUpgradeInternal();
	}
}
