using HextechRunes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace HextechRunesSponsorPack;

public abstract class ConditionalEnchantmentForgeBase : HextechForgeBase
{
	private const int SelectionCount = 1;

	protected abstract IReadOnlyList<ConditionalEnchantmentOption> Options { get; }

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. Options.SelectMany(option => option.CreateHoverTips())
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
			CanEnchantWithMappedOption);
		foreach (CardModel selected in selectedCards)
		{
			if (!TryGetOption(selected, out ConditionalEnchantmentOption? option))
			{
				continue;
			}

			ConditionalEnchantmentOption resolvedOption = option!;
			Flash();
			CardCmd.Enchant(resolvedOption.CreateCanonical().ToMutable(), selected, resolvedOption.Amount);
			CardCmd.Preview(selected);
		}
	}

	protected static ConditionalEnchantmentOption For<TEnchantment>(CardType cardType, int amount = 1)
		where TEnchantment : EnchantmentModel
	{
		return new ConditionalEnchantmentOption(
			cardType,
			amount,
			() => ModelDb.Enchantment<TEnchantment>(),
			() => HoverTipFactory.FromEnchantment<TEnchantment>(amount));
	}

	private bool CanEnchantWithMappedOption(CardModel card)
	{
		return TryGetOption(card, out _);
	}

	private bool TryGetOption(CardModel card, out ConditionalEnchantmentOption? option)
	{
		option = Options.FirstOrDefault(candidate =>
			card.Type == candidate.CardType
			&& candidate.CreateCanonical().CanEnchant(card));
		return option != null;
	}

	protected sealed record ConditionalEnchantmentOption(
		CardType CardType,
		int Amount,
		Func<EnchantmentModel> CreateCanonical,
		Func<IEnumerable<IHoverTip>> CreateHoverTips);
}

public sealed class MysticForge : ConditionalEnchantmentForgeBase
{
	protected override IReadOnlyList<ConditionalEnchantmentOption> Options { get; } =
	[
		For<Instinct>(CardType.Attack),
		For<Imbued>(CardType.Skill),
		For<Sown>(CardType.Power)
	];
}

public sealed class EnchantmentForge : ConditionalEnchantmentForgeBase
{
	protected override IReadOnlyList<ConditionalEnchantmentOption> Options { get; } =
	[
		For<Sharp>(CardType.Attack, 3),
		For<Nimble>(CardType.Skill, 3),
		For<Swift>(CardType.Power, 3)
	];
}
