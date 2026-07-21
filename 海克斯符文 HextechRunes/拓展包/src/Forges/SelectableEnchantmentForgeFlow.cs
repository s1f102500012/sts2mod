using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunesSponsorPack;

internal sealed record SelectableEnchantmentOption(
	Func<RelicModel> CreateChoiceRelic,
	Func<EnchantmentModel> CreateCanonical,
	Func<IEnumerable<IHoverTip>> CreateHoverTips)
{
	internal static SelectableEnchantmentOption For<TEnchantment>(Func<RelicModel> createChoiceRelic)
		where TEnchantment : EnchantmentModel
	{
		return new SelectableEnchantmentOption(
			createChoiceRelic,
			() => ModelDb.Enchantment<TEnchantment>(),
			() => HoverTipFactory.FromEnchantment<TEnchantment>());
	}
}

internal static class SelectableEnchantmentForgeFlow
{
	internal static bool CanEnchantWithAnyOption(CardModel card, IReadOnlyList<SelectableEnchantmentOption> options)
	{
		return options.Any(option => option.CreateCanonical().CanEnchant(card));
	}

	internal static IReadOnlyList<SelectableEnchantmentOption> GetApplicableOptions(
		CardModel card,
		IReadOnlyList<SelectableEnchantmentOption> options)
	{
		return options
			.Where(option => option.CreateCanonical().CanEnchant(card))
			.ToArray();
	}

	internal static async Task<EnchantmentModel?> SelectEnchantment(
		Player owner,
		CardModel card,
		IReadOnlyList<SelectableEnchantmentOption> options,
		string choiceContext)
	{
		IReadOnlyList<SelectableEnchantmentOption> applicable = GetApplicableOptions(card, options);
		if (applicable.Count == 0)
		{
			return null;
		}

		IReadOnlyList<RelicModel> choiceRelics = applicable
			.Select(static option => option.CreateChoiceRelic())
			.ToArray();
		RelicModel? selected = await HextechRunesApi.SelectRelicOption(owner, choiceRelics, choiceContext);
		int selectedIndex = IndexOfModel(choiceRelics, selected);
		return selectedIndex >= 0 && selectedIndex < applicable.Count
			? applicable[selectedIndex].CreateCanonical()
			: null;
	}

	internal static int IndexOfModel(IReadOnlyList<RelicModel> options, RelicModel? selected)
	{
		if (selected == null)
		{
			return -1;
		}

		ModelId selectedId = selected.CanonicalInstance?.Id ?? selected.Id;
		for (int i = 0; i < options.Count; i++)
		{
			ModelId optionId = options[i].CanonicalInstance?.Id ?? options[i].Id;
			if (optionId == selectedId)
			{
				return i;
			}
		}

		return -1;
	}
}
