using HextechRunes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunesSponsorPack;

public sealed class EntropyForge : HextechForgeBase
{
	private const int SelectionCount = 1;

	private static readonly IReadOnlyList<SelectableEnchantmentOption> EnchantmentOptions =
	[
		SelectableEnchantmentOption.For<EntropyIncrease>(() => ModelDb.Relic<EntropyIncreaseChoiceRelic>()),
		SelectableEnchantmentOption.For<EntropyDecrease>(() => ModelDb.Relic<EntropyDecreaseChoiceRelic>())
	];

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. EnchantmentOptions.SelectMany(static option => option.CreateHoverTips())
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckGeneric(
			Owner,
			new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, SelectionCount),
			CanEnchantWithAnyOption);
		CardModel? selectedCard = selectedCards.FirstOrDefault();
		if (selectedCard == null)
		{
			return;
		}

		EnchantmentModel? selectedEnchantment = await SelectableEnchantmentForgeFlow.SelectEnchantment(
			Owner,
			selectedCard,
			EnchantmentOptions,
			$"entropy-forge-enchantment-choice card={(selectedCard.CanonicalInstance?.Id ?? selectedCard.Id).Entry}");
		if (selectedEnchantment == null)
		{
			return;
		}

		Flash();
		CardCmd.Enchant(selectedEnchantment.ToMutable(), selectedCard, SelectionCount);
		CardCmd.Preview(selectedCard);
	}

	private static bool CanEnchantWithAnyOption(CardModel card)
	{
		return SelectableEnchantmentForgeFlow.CanEnchantWithAnyOption(card, EnchantmentOptions);
	}
}

public sealed class EntropyIncreaseChoiceRelic : GoldForgeChoiceRelic
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromEnchantment<EntropyIncrease>()
	];
}

public sealed class EntropyDecreaseChoiceRelic : GoldForgeChoiceRelic
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromEnchantment<EntropyDecrease>()
	];
}
