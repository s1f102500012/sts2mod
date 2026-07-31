namespace HextechRunes;

public sealed class OkBoomerangCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.OkBoomerangCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(6m, ValueProp.Move),
		new DynamicVar("Hits", 2m)
	];

	public OkBoomerangCard()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner?.Creature.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		// 表现:掷镖动作只在起手播一次,镖沿弧线依次扫过所有敌人,
		// 在最远处折返后逆序再扫一遍(去程第1击,回程第2击)。
		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", 0f);
		HextechCombatVfx.BoomerangSweep(Owner.Creature, enemies, ModelDb.Relic<OkBoomerangRune>().Icon, roundTrip: true);

		// 只等镖飞抵第一个敌人;之后每次结算自带的 0.2s 标准尾巴
		// 与镖的每敌节拍(BoomerangPerTargetSeconds)一致,连续结算即可同步。
		await Cmd.CustomScaledWait(
			HextechCombatVfx.BoomerangFirstArrivalSeconds,
			HextechCombatVfx.BoomerangFirstArrivalSeconds);

		foreach (Creature enemy in enemies)
		{
			await StrikeIfHittable(choiceContext, cardPlay, enemy);
		}

		for (int i = enemies.Count - 1; i >= 0; i--)
		{
			await StrikeIfHittable(choiceContext, cardPlay, enemies[i]);
		}
	}

	private async Task StrikeIfHittable(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature enemy)
	{
		if (enemy.IsDead || Owner?.Creature.CombatState == null)
		{
			return;
		}

		// FromCard 默认会给每次结算挂 Attack 动画+等待,这里全部取消:
		// 起手动画只在 OnPlay 掷镖时播一次,伤害严格跟随镖的到达节拍瞬时结算。
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCardCompat(this, cardPlay)
			.WithAttackerAnim(null, 0f)
			.Targeting(enemy)
			.Execute(choiceContext);
	}

	// 打出后返回手牌:回力镖掷出必归。(0.108.0 起该虚方法改为返回含位置的元组,0.109.0 起改为 CardLocation)
#if STS2_109_OR_NEWER
	protected override CardLocation GetResultLocationForCardPlay()
	{
		CardLocation location = base.GetResultLocationForCardPlay();
		location.pileType = PileType.Hand;
		location.position = CardPilePosition.Bottom;
		return location;
	}
#elif STS2_108_OR_NEWER
	protected override (PileType, CardPilePosition) GetResultPileTypeAndPositionForCardPlay()
	{
		return (PileType.Hand, CardPilePosition.Bottom);
	}
#else
	protected override PileType GetResultPileTypeForCardPlay()
	{
		return PileType.Hand;
	}
#endif

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
