using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

public sealed class SearingAttackRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<SearingAttackCard>(upgrade: true)
	];

	public override Task AfterObtained()
	{
		return AddCardCopiesToDeckOrHand<SearingAttackCard>(
			DynamicVars.Cards.IntValue,
			UpgradeGrantedCard);
	}

	internal static void UpgradeGrantedCard(CardModel card)
	{
		CardCmd.Upgrade(card, CardPreviewStyle.None);
	}
}
