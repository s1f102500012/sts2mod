using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace UniversalDominionSword;

public sealed class UniversalDominionSwordRelic : RelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	public override string PackedIconPath => ModInfo.RelicIconPath;

	protected override string PackedIconOutlinePath => PackedIconPath;

	protected override string BigIconPath => PackedIconPath;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
		HoverTipFactory.FromCardWithCardHoverTips<UniversalDominionSwordCard>();

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		CardModel card = Owner.RunState.CreateCard(
			ModelDb.Card<UniversalDominionSwordCard>(),
			Owner);

		CardPileAddResult result = await CardPileCmd.Add(
			card,
			PileType.Deck,
			CardPilePosition.Bottom);

		if (result.success)
		{
			SaveManager.Instance.MarkCardAsSeen(result.cardAdded);
			Flash();
			CardCmd.PreviewCardPileAdd([result], 2f);
		}
	}
}
