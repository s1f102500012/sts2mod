using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;

namespace IntegratedStrategyEvents.Events;

public sealed partial class GoodsFromTheMouthEvent : IntegratedStrategyEventModel
{
	private const int LaborSavingGoldCost = 50;
	private const int ValuePreservingGoldCost = 100;
	private const int TreeSeaSouvenirGoldCost = 150;
	private const int RareCardRewardOptionCount = 3;

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		Player owner = OwnerOrThrow;
		return
		[
			GoldChoice(owner, LaborSavingGoldCost, BuyLaborSavingItem, "LABOR_SAVING", "LABOR_SAVING_LOCKED"),
			GoldChoice(owner, ValuePreservingGoldCost, BuyValuePreservingItem, "VALUE_PRESERVING", "VALUE_PRESERVING_LOCKED"),
			GoldChoice(owner, TreeSeaSouvenirGoldCost, BuyTreeSeaSouvenir, "TREE_SEA_SOUVENIR", "TREE_SEA_SOUVENIR_LOCKED"),
			Choice(Leave, "LEAVE")
		];
	}

	private async Task BuyLaborSavingItem()
	{
		await SpendGold(LaborSavingGoldCost);
		await OfferRandomPotionReward(PotionRarity.Rare);
		Finish("LABOR_SAVING");
	}

	private async Task BuyValuePreservingItem()
	{
		await SpendGold(ValuePreservingGoldCost);
		Finish("VALUE_PRESERVING");
		await OfferRareCardReward(RareCardRewardOptionCount);
	}

	private async Task BuyTreeSeaSouvenir()
	{
		await SpendGold(TreeSeaSouvenirGoldCost);
		await ObtainRandomRelic();
		Finish("TREE_SEA_SOUVENIR");
	}

	private Task Leave()
	{
		Finish("LEAVE");
		return Task.CompletedTask;
	}
}
