namespace HextechRunes;

public sealed class FeelTheBurnCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.FeelTheBurnCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<HextechBurnPower>(5m)
	];

	public FeelTheBurnCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner?.Creature.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.Enemies
			.Where(static enemy => enemy.IsAlive)
			.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		// 移除全部增益(受保护的怪物机制与战利品结算 buff 除外,与升级:暴露同口径)。
		foreach (Creature enemy in enemies)
		{
			List<PowerModel> buffs = enemy.Powers
				.Where(static power => power.GetTypeForAmount(power.Amount) == PowerType.Buff
					&& !HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(power))
				.ToList();
			foreach (PowerModel power in buffs)
			{
				await HextechMonsterInteractionPolicy.RemoveMonsterBuffSafely(power);
			}
		}

		await PowerCmd.Apply<HextechBurnPower>(enemies, DynamicVars["HextechBurnPower"].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars["HextechBurnPower"].UpgradeValueBy(5m);
	}
}
