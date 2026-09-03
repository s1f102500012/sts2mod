using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

public sealed class CosplayRune : HextechRelicBase
{
	private static readonly Type[] RelicTypes =
	[
		typeof(Lantern)
	];

	public override bool HasUponPickupEffect => true;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedCosplayInnateMarker
	{
		get => 0;
		set { }
	}

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<FeelNoPain>(),
		HoverTipFactory.FromCard<Juggernaut>(),
		HoverTipFactory.FromCard<Stoke>(),
		HoverTipFactory.FromCard<BattleTrance>(),
		HoverTipFactory.FromKeyword(CardKeyword.Innate),
		.. HoverTipFactory.FromRelic<Lantern>()
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		await AddInnateCard<FeelNoPain>();
		await AddInnateCard<Juggernaut>();
		await AddInnateCard<Stoke>();
		await AddInnateCard<BattleTrance>();
		await RelicBundleGrantHelper.GrantRelics(Owner, RelicTypes);
	}

	private Task AddInnateCard<TCard>()
		where TCard : CardModel
	{
		return AddCardCopiesToDeckOrHand<TCard>(1, static card =>
		{
			ApplyPersistentInnate(card);
		});
	}

	public override Task AfterCardEnteredCombat(CardModel card)
	{
		if (Owner == null || card.Owner != Owner)
		{
			return Task.CompletedTask;
		}

		if (HextechRunesApi.IsPersistentInnateTracked(card.DeckVersion))
		{
			HextechRunesApi.RestorePersistentInnate(card);
		}

		return Task.CompletedTask;
	}

	private static void ApplyPersistentInnate(CardModel card)
	{
		HextechRunesApi.TrackPersistentInnate(card);
		CardCmd.ApplyKeyword(card, CardKeyword.Innate);
	}
}
