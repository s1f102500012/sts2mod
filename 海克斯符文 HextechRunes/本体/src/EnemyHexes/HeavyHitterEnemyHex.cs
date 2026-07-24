namespace HextechRunes;

internal sealed class HeavyHitterEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.HeavyHitter;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return dealer == null ? 1m : ResolveMultiplier(dealer.MaxHp, context.ScalingPlayerCount);
	}

	internal static decimal ResolveMultiplier(decimal maxHp, int playerCount = 1)
	{
		decimal hpPerPercent = 15m * Math.Clamp(playerCount, 1, 16);
		decimal fifteenHpSteps = Math.Max(0m, Math.Floor(maxHp / hpPerPercent));
		return 1m + Math.Min(30m, fifteenHpSteps) * 0.01m;
	}
}
