namespace HextechRunes;

public sealed class SearingAttackCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.SearingAttackCardPortraitPath;

	public override int MaxUpgradeLevel => Math.Max(CurrentUpgradeLevel, HextechStarterUpgradeHooks.UpgradeLevelCap);

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(12m, ValueProp.Move)
	];

	public SearingAttackCard()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, shouldShowInCardLibrary: true)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return cardPlay.Target == null
			? Task.CompletedTask
			: HextechGameApiCompat.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		decimal previousDamage = DamageForUpgradeLevel(CurrentUpgradeLevel - 1);
		decimal targetDamage = DamageForUpgradeLevel(CurrentUpgradeLevel);
		DynamicVars.Damage.UpgradeValueBy(targetDamage - previousDamage);
	}

	private static decimal DamageForUpgradeLevel(int upgradeLevel)
	{
		return upgradeLevel <= 0
			? 12m
			: upgradeLevel * (upgradeLevel + 7m) / 2m + 12m;
	}
}
