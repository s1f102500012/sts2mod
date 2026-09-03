using HextechRunes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunesSponsorPack;

public sealed class DollysMirrorForge : HextechForgeBase
{
	internal const int RelicsPerPage = 6;

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
		int pageIndex = 0;
		RelicModel? selected;
		while (true)
		{
			DollyRelicPage page = CreateRelicPage(options, pageIndex);
			selected = await HextechRunesApi.SelectRelicOption(
				Owner!,
				page.Options,
				$"dollys-mirror-relic-choice page={page.PageIndex + 1}/{page.PageCount}");
			if (selected is DollyPreviousPageRelic)
			{
				pageIndex = page.PageIndex - 1;
				continue;
			}
			if (selected is DollyNextPageRelic)
			{
				pageIndex = page.PageIndex + 1;
				continue;
			}
			break;
		}

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
			.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).ToString(), StringComparer.Ordinal)
			.ToArray();
	}

	internal static DollyRelicPage CreateRelicPage(IReadOnlyList<RelicModel> relics, int requestedPageIndex)
	{
		ArgumentNullException.ThrowIfNull(relics);
		DollyRelicPageLayout layout = GetRelicPageLayout(relics.Count, requestedPageIndex);
		List<RelicModel> pageOptions = [];
		if (layout.HasPreviousPage)
		{
			pageOptions.Add(ModelDb.Relic<DollyPreviousPageRelic>());
		}

		pageOptions.AddRange(relics.Skip(layout.StartIndex).Take(layout.RelicCount));
		if (layout.HasNextPage)
		{
			pageOptions.Add(ModelDb.Relic<DollyNextPageRelic>());
		}

		return new DollyRelicPage(pageOptions, layout.PageIndex, layout.PageCount);
	}

	internal static DollyRelicPageLayout GetRelicPageLayout(int relicCount, int requestedPageIndex)
	{
		if (relicCount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(relicCount));
		}

		int pageCount = Math.Max(1, (relicCount + RelicsPerPage - 1) / RelicsPerPage);
		int pageIndex = Math.Clamp(requestedPageIndex, 0, pageCount - 1);
		int startIndex = pageIndex * RelicsPerPage;
		return new DollyRelicPageLayout(
			startIndex,
			Math.Min(RelicsPerPage, Math.Max(0, relicCount - startIndex)),
			pageIndex > 0,
			pageIndex + 1 < pageCount,
			pageIndex,
			pageCount);
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

internal readonly record struct DollyRelicPage(
	IReadOnlyList<RelicModel> Options,
	int PageIndex,
	int PageCount);

internal readonly record struct DollyRelicPageLayout(
	int StartIndex,
	int RelicCount,
	bool HasPreviousPage,
	bool HasNextPage,
	int PageIndex,
	int PageCount);

public sealed class DollyCardChoiceRelic : PrismaticForgeChoiceRelic
{
}

public sealed class DollyRelicChoiceRelic : PrismaticForgeChoiceRelic
{
}

public sealed class DollyPreviousPageRelic : PrismaticForgeChoiceRelic
{
}

public sealed class DollyNextPageRelic : PrismaticForgeChoiceRelic
{
}
