namespace HextechRunes;

public sealed class AllInCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.AllInCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(12m, ValueProp.Move)
	];

	public AllInCard()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Target == null)
		{
			return;
		}

		await HextechGameApiCompat.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage, Owner.Creature, this);

		List<CardModel> nonAttackCards = PileType.Hand.GetPile(Owner).Cards
			.Where(static card => card.Type != CardType.Attack)
			.ToList();
		if (nonAttackCards.Count > 0)
		{
			await CardCmd.Discard(choiceContext, nonAttackCards);
		}
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
