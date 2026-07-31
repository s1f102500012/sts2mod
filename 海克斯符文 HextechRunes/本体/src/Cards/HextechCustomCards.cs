namespace HextechRunes;

public sealed class ElicitCard : HextechOwnerPoolTokenCard
{
	public override OrbEvokeType OrbEvokeType => OrbEvokeType.All;

	public override string PortraitPath => HextechAssets.ElicitCardPortraitPath;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.Static(StaticHoverTip.Evoke)
	];

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Retain,
		CardKeyword.Exhaust
	];

	public ElicitCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int orbCount = Owner.PlayerCombatState?.OrbQueue.Orbs.Count ?? 0;
		for (int i = 0; i < orbCount; i++)
		{
			await OrbCmd.EvokeNext(choiceContext, Owner);
		}
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}
}

public sealed class TrickMagicCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.TrickMagicCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Exhaust
	];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2),
		new PowerVar<BufferPower>(1m),
		new DynamicVar("Replays", 1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<BufferPower>(),
		HoverTipFactory.FromPower<HextechAttackReplayPower>()
	];

	public TrickMagicCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner, fromHandDraw: false);
		await PowerCmd.Apply<BufferPower>(Owner.Creature, DynamicVars["BufferPower"].BaseValue, Owner.Creature, this);
		await PowerCmd.Apply<HextechAttackReplayPower>(Owner.Creature, DynamicVars["Replays"].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars["BufferPower"].UpgradeValueBy(1m);
	}
}

public sealed class CatalystCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.CatalystCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Exhaust
	];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("PoisonMultiplier", 2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PoisonPower>()
	];

	public CatalystCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.AnyEnemy, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Target == null || Owner == null)
		{
			return;
		}

		decimal poison = cardPlay.Target.GetPowerAmount<PoisonPower>();
		decimal multiplier = DynamicVars["PoisonMultiplier"].BaseValue;
		decimal additionalPoison = poison * (multiplier - 1m);
		if (additionalPoison <= 0m)
		{
			return;
		}

		await PowerCmd.Apply<PoisonPower>(cardPlay.Target, additionalPoison, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars["PoisonMultiplier"].UpgradeValueBy(1m);
	}
}
