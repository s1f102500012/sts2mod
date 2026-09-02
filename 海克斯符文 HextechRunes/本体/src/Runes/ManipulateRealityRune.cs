using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

public sealed class ManipulateRealityRune : HextechRelicBase
{
	public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
	{
		if (card.Owner != Owner || !card.IsUpgradable)
		{
			return Task.CompletedTask;
		}

		CardCmd.Upgrade(card, CardPreviewStyle.None);
		Flash();
		return Task.CompletedTask;
	}
}
