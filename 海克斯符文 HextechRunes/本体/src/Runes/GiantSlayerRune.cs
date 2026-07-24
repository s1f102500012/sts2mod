namespace HextechRunes;

public sealed class GiantSlayerRune : HextechRelicBase
{
	internal const int EnemyMaxHpPerPercent = 8;
	internal const decimal DamagePerStepPercent = 0.01m;
	internal const decimal MaximumBonusPercent = 0.5m;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2),
		new DynamicVar("EnemyMaxHpPerPercent", EnemyMaxHpPerPercent),
		new DynamicVar("DamagePerStepPercent", DamagePerStepPercent),
		new DynamicVar("MaxBonusPercent", MaximumBonusPercent),
		new DynamicVar("Scale", 0.65m)
	];

	internal float BodyScaleDelta => (float)DynamicVars["Scale"].BaseValue - 1f;

	public override Task AfterObtained()
	{
		HextechPlayerBodyScaleHelper.Update(Owner);
		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		HextechPlayerBodyScaleHelper.Update(Owner);
		return Task.CompletedTask;
	}

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner)
		{
			return count;
		}

		return count + DynamicVars.Cards.BaseValue;
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null || target?.Side != CombatSide.Enemy || !IsDamageFromOwner(dealer, cardSource))
		{
			return 1m;
		}

		return ResolveDamageMultiplier(
			target.MaxHp,
			DynamicVars["EnemyMaxHpPerPercent"].IntValue,
			DynamicVars["DamagePerStepPercent"].BaseValue,
			DynamicVars["MaxBonusPercent"].BaseValue);
	}

	internal static decimal ResolveDamageMultiplier(
		int enemyMaxHp,
		int hpPerStep = EnemyMaxHpPerPercent,
		decimal damagePerStep = DamagePerStepPercent,
		decimal maximumBonus = MaximumBonusPercent)
	{
		int steps = Math.Max(0, enemyMaxHp) / Math.Max(1, hpPerStep);
		decimal bonus = Math.Min(steps * damagePerStep, maximumBonus);
		return 1m + Math.Max(0m, bonus);
	}
}
