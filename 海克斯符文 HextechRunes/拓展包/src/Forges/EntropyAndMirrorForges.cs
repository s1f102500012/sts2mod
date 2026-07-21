using HextechRunes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunesSponsorPack;

public sealed class DollysMirrorForge : HextechForgeBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromRelic<DollyCardChoiceRelic>(),
		.. HoverTipFactory.FromRelic<DollyRelicChoiceRelic>()
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		List<RelicModel> categoryOptions = [];
		if (Owner.Deck.Cards.Count > 0)
		{
			categoryOptions.Add(ModelDb.Relic<DollyCardChoiceRelic>());
		}
		if (CreateRelicOptions().Count > 0)
		{
			categoryOptions.Add(ModelDb.Relic<DollyRelicChoiceRelic>());
		}
		if (categoryOptions.Count == 0)
		{
			return;
		}

		RelicModel? selectedCategory = await HextechRunesApi.SelectRelicOption(
			Owner,
			categoryOptions,
			"dollys-mirror-category-choice");
		if (selectedCategory is DollyCardChoiceRelic)
		{
			await CopySelectedCard();
		}
		else if (selectedCategory is DollyRelicChoiceRelic)
		{
			await CopySelectedRelic();
		}
	}

	private async Task CopySelectedCard()
	{
		IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckGeneric(
			Owner!,
			new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1),
			static _ => true);
		CardModel? selected = selectedCards.FirstOrDefault();
		if (selected == null)
		{
			return;
		}

		Flash();
		CardModel copy = Owner!.RunState.CloneCard(selected);
		CardPileAddResult result = await CardPileCmd.Add(copy, PileType.Deck);
		if (result.success)
		{
			SaveManager.Instance.MarkCardAsSeen(result.cardAdded);
			CardCmd.PreviewCardPileAdd(result, 2f);
		}
	}

	private async Task CopySelectedRelic()
	{
		IReadOnlyList<RelicModel> options = CreateRelicOptions();
		RelicModel? selected = await HextechRunesApi.SelectRelicOption(
			Owner!,
			options,
			"dollys-mirror-relic-choice");
		if (selected == null)
		{
			return;
		}

		Flash();
		RelicModel canonical = ModelDb.GetById<RelicModel>(selected.CanonicalInstance?.Id ?? selected.Id);
		await RelicCmd.Obtain(canonical.ToMutable(), Owner!);
	}

	private IReadOnlyList<RelicModel> CreateRelicOptions()
	{
		if (Owner == null)
		{
			return [];
		}

		return Owner.Relics
			.Where(IsNonHextechRelic)
			.GroupBy(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.Select(static group => group.First())
			.ToArray();
	}

	private static bool IsNonHextechRelic(RelicModel relic)
	{
		Type relicType = (relic.CanonicalInstance ?? relic).GetType();
		return !typeof(HextechRelicBase).IsAssignableFrom(relicType) && !IsHextechType(relicType);
	}

	private static bool IsHextechType(Type type)
	{
		string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
		return assemblyName is "HextechRunes" or "HextechRunesSponsorPack"
			|| type.Namespace?.StartsWith("HextechRunes", StringComparison.Ordinal) is true;
	}
}

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

public abstract class SponsorForgeChoiceRelic : RelicModel
{
	public override RelicRarity Rarity => RelicRarity.Event;
}

public abstract class PrismaticForgeChoiceRelic : SponsorForgeChoiceRelic
{
	private const string ChoiceIconPath = "res://HextechRunes/images/relics/prismaticForge.png";

	public override string PackedIconPath => ChoiceIconPath;

	protected override string PackedIconOutlinePath => ChoiceIconPath;

	protected override string BigIconPath => ChoiceIconPath;
}

public abstract class GoldForgeChoiceRelic : SponsorForgeChoiceRelic
{
	private const string ChoiceIconPath = "res://HextechRunes/images/relics/goldForge.png";

	public override string PackedIconPath => ChoiceIconPath;

	protected override string PackedIconOutlinePath => ChoiceIconPath;

	protected override string BigIconPath => ChoiceIconPath;
}

public sealed class DollyCardChoiceRelic : PrismaticForgeChoiceRelic
{
}

public sealed class DollyRelicChoiceRelic : PrismaticForgeChoiceRelic
{
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
