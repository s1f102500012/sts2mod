using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunesSponsorPack;

public sealed class BasicForge : HextechForgeBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromRelic<PaelsClaw>(),
		.. HoverTipFactory.FromRelic<NutritiousSoup>()
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		IReadOnlyList<RelicModel> choiceRelics = CreateChoiceRelics();
		RelicModel? selected = await HextechRunesApi.SelectRelicOption(Owner, choiceRelics, "basic-forge-relic-choice");
		if (selected == null)
		{
			return;
		}

		Flash();
		await RelicCmd.Obtain(selected.ToMutable(), Owner);
	}

	private static IReadOnlyList<RelicModel> CreateChoiceRelics()
	{
		return
		[
			ModelDb.Relic<PaelsClaw>(),
			ModelDb.Relic<NutritiousSoup>()
		];
	}
}
