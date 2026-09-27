namespace HextechRunes;

public abstract class DragonSoulRuneBase<TCard> : HextechRelicBase
	where TCard : CardModel
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<TCard>()
	];

	// 泛型模型类的钩子只转发,不直接用类型参数(第三方批量补丁会把它写死,见 AutoPlayFormsAtCombatStartRuneBase)。
	public override Task AfterObtained()
	{
		return GrantDragonSoulCardsAsync();
	}

	private async Task GrantDragonSoulCardsAsync()
	{
		Flash();
		await AddCardCopiesToDeckOrHand<TCard>(DynamicVars.Cards.IntValue);
	}
}
