using MegaCrit.Sts2.Core.Models.CardPools;

namespace HextechRunes;

public sealed class SearingAttackCard : CardModel
{
	public override CardPoolModel Pool => IsMutable && Owner != null
		? Owner.Character.CardPool
		: ModelDb.CardPool<TokenCardPool>();

	public override CardPoolModel VisualCardPool => Pool;

	public override string PortraitPath => HextechAssets.SearingAttackCardPortraitPath;

	public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

	// 上限护栏:第三方 mod 存在「while IsUpgradable 升到满」式逻辑,999 会被一口气拉满
	// (升级:打击/防御曾因此炸出 3003 伤害,玩家实报)。正常一场战斗打不出 200 次,封顶无感。
	public override int MaxUpgradeLevel => Math.Max(CurrentUpgradeLevel, 200);

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
