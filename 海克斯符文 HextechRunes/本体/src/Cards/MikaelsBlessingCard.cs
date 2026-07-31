namespace HextechRunes;

public sealed class MikaelsBlessingCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.MikaelsBlessingCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Retain
	];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HealPercent", 10m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromKeyword(CardKeyword.Retain)
	];

	public MikaelsBlessingCard()
		: base(1, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<Creature> affectedCreatures = Owner.RunState.Players
			.Select(static player => player.Creature)
			.Where(static creature => !creature.IsDead)
			.ToList();
		foreach (Creature creature in affectedCreatures)
		{
			HextechMikaelsBlessingVfx.Play(creature);
		}

		int healAmount = Math.Max(1, (int)Math.Floor(Owner.Creature.MaxHp * DynamicVars["HealPercent"].BaseValue / 100m));
		await CreatureCmd.Heal(Owner.Creature, healAmount);

		foreach (Creature creature in affectedCreatures)
		{
			List<PowerModel> negativePowers = creature.Powers
				.Where(static power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
				.ToList();
			foreach (PowerModel power in negativePowers)
			{
				await PowerCmd.Remove(power);
			}
		}
	}

	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
	}
}
