using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace HextechRunes;

internal static class HextechKnifeHelper
{
	public static bool IsShivLike(CardModel? card, Player? owner)
	{
		if (card == null)
		{
			return false;
		}

		if (card is SovereignBlade && owner?.GetRelic<BigKnifeRune>() != null)
		{
			return true;
		}

		return card.Tags.Contains(CardTag.Shiv);
	}

	public static bool ShouldTreatSovereignBladeAsShiv(CardModel card, Player? owner)
	{
		return card is SovereignBlade && owner?.GetRelic<BigKnifeRune>() != null;
	}

	public static bool ShouldFanOfKnivesAffectSovereignBlade(SovereignBlade card)
	{
		Player? owner;
		try
		{
			owner = card.Owner;
		}
		catch (CanonicalModelException)
		{
			return false;
		}

		return ShouldTreatSovereignBladeAsShiv(card, owner)
			&& owner.Creature.HasPower<FanOfKnivesPower>();
	}

	public static bool TryCreateBigKnifeReplacement(CardModel card, out CardModel replacement)
	{
		replacement = card;
		if (card is not Shiv)
		{
			return false;
		}

		Player? owner;
		try
		{
			owner = card.Owner;
		}
		catch (CanonicalModelException)
		{
			return false;
		}

		if (owner?.GetRelic<BigKnifeRune>() == null || owner.Creature.CombatState is not CombatState combatState)
		{
			return false;
		}

		SovereignBlade blade = combatState.CreateCard<SovereignBlade>(owner);
		if (card.IsUpgraded && blade.IsUpgradable)
		{
			CardCmd.Upgrade(blade, CardPreviewStyle.None);
		}

		ConfigureBigKnifeBlade(blade);
		replacement = blade;
		return true;
	}

	public static void ConfigureBigKnifeBlade(CardModel card)
	{
		card.SetToFreeThisTurn();
		card.ExhaustOnNextPlay = true;
		if (!card.Keywords.Contains(CardKeyword.Exhaust))
		{
			card.AddKeyword(CardKeyword.Exhaust);
		}
		InkshadowRune.TryApplyForOwner(card, card.Owner);
		card.InvokeEnergyCostChanged();
	}

	public static async Task<CardModel?> CreateOneBigKnifeBladeInHand(Player owner, CombatState combatState)
	{
		IEnumerable<CardModel> cards = await CreateBigKnifeBladesInHand(owner, 1, combatState);
		return cards.FirstOrDefault();
	}

	public static async Task<IEnumerable<CardModel>> CreateBigKnifeBladesInHand(Player owner, int count, CombatState combatState)
	{
		if (count <= 0 || CombatManager.Instance.IsOverOrEnding)
		{
			return Array.Empty<CardModel>();
		}

		List<CardModel> blades = new(count);
		for (int i = 0; i < count; i++)
		{
			SovereignBlade blade = combatState.CreateCard<SovereignBlade>(owner);
			ConfigureBigKnifeBlade(blade);
			blades.Add(blade);
		}

		await HextechCardGeneration.AddGeneratedCardsToCombat(blades, PileType.Hand, addedByPlayer: true);
		return blades;
	}
}
