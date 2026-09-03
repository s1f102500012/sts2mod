using HextechRunes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace HextechRunesSponsorPack;

public sealed class ArcaneForge : HextechForgeBase
{
	private const int SelectionCount = 1;

	private static readonly IReadOnlyList<SelectableEnchantmentOption> EnchantmentOptions =
	[
		SelectableEnchantmentOption.For<Clone>(() => ModelDb.Relic<ArcaneCloneChoiceRelic>()),
		SelectableEnchantmentOption.For<SoulsPower>(() => ModelDb.Relic<ArcaneSoulsPowerChoiceRelic>()),
		SelectableEnchantmentOption.For<RoyallyApproved>(() => ModelDb.Relic<ArcaneRoyallyApprovedChoiceRelic>())
	];

	public override bool HasUponPickupEffect => true;

	public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
	{
		if (Owner == null || player != Owner || options.Any(static option => option.OptionId == "CLONE"))
		{
			return false;
		}

		if (!Owner.Deck.Cards.Any(HasCloneEnchantment))
		{
			return false;
		}

		options.Add(new CloneRestSiteOption(player));
		return true;
	}

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
		foreach (CardModel selectedCard in selectedCards)
		{
			EnchantmentModel? selectedEnchantment = await SelectableEnchantmentForgeFlow.SelectEnchantment(
				Owner,
				selectedCard,
				EnchantmentOptions,
				$"arcane-forge-enchantment-choice card={(selectedCard.CanonicalInstance?.Id ?? selectedCard.Id).Entry}");
			if (selectedEnchantment == null)
			{
				continue;
			}

			Flash();
			CardCmd.Enchant(selectedEnchantment.ToMutable(), selectedCard, SelectionCount);
			CardCmd.Preview(selectedCard);
		}
	}

	private static bool CanEnchantWithAnyOption(CardModel card)
	{
		return SelectableEnchantmentForgeFlow.CanEnchantWithAnyOption(card, EnchantmentOptions);
	}

	private static bool HasCloneEnchantment(CardModel card)
	{
		// 原版语义:card.Enchantment 就是这张牌的附魔。装了多重附魔类模组时由它们的 IL 重写器接管这种写法。
		return card.Enchantment is Clone;
	}

}

public abstract class ArcaneEnchantmentChoiceRelic<TEnchantment> : RelicModel
	where TEnchantment : EnchantmentModel
{
	private const string ChoiceIconPath = "res://HextechRunes/images/relics/prismaticForge.png";

	public override RelicRarity Rarity => RelicRarity.Event;

	public override string PackedIconPath => ChoiceIconPath;

	protected override string PackedIconOutlinePath => ChoiceIconPath;

	protected override string BigIconPath => ChoiceIconPath;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromEnchantment<TEnchantment>()
	];
}

public sealed class ArcaneCloneChoiceRelic : ArcaneEnchantmentChoiceRelic<Clone>
{
}

public sealed class ArcaneSoulsPowerChoiceRelic : ArcaneEnchantmentChoiceRelic<SoulsPower>
{
}

public sealed class ArcaneRoyallyApprovedChoiceRelic : ArcaneEnchantmentChoiceRelic<RoyallyApproved>
{
}
