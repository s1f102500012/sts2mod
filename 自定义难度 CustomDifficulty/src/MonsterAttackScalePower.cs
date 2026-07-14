using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CustomDifficulty;

public sealed class MonsterAttackScalePower : PowerModel
{
	// 旧编码区间（0.1.x 固定倍率模式）：Amount = 100 + ticks，ticks 1..50 → x0.1..x5.0。
	private const int EncodedAttackTicksOffset = 100;

	// 新编码区间（0.2.0 起，支持递进模式的细粒度倍率）：Amount = 10000 + 倍率百分比。
	// 与旧区间不重叠，旧档里带旧编码的 power 仍按旧逻辑解出。
	private const int EncodedPercentOffset = 10000;
	private const int MinEncodedPercent = 10;
	private const int MaxEncodedPercent = 9900;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public static int EncodeAttackTicks(int attackTicks)
	{
		return EncodedAttackTicksOffset + CustomDifficultySettings.NormalizeTicks(attackTicks);
	}

	public static int EncodeMultiplierPercent(decimal multiplier)
	{
		int percent = (int)Math.Round(multiplier * 100m, MidpointRounding.AwayFromZero);
		return EncodedPercentOffset + Math.Clamp(percent, MinEncodedPercent, MaxEncodedPercent);
	}

#if STS2_108_OR_NEWER
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
#endif
	{
		if (dealer != Owner || target == null || !target.IsPlayer)
		{
			return 1m;
		}

		if (!props.IsPoweredAttack())
		{
			return 1m;
		}

		decimal multiplier = GetMultiplier();
		return multiplier > 0m ? multiplier : 1m;
	}

	private decimal GetMultiplier()
	{
		if (Amount >= EncodedPercentOffset)
		{
			return (Amount - EncodedPercentOffset) / 100m;
		}

		if (Amount > EncodedAttackTicksOffset)
		{
			return CustomDifficultySettings.TicksToMultiplier(
				CustomDifficultySettings.NormalizeTicks(Amount - EncodedAttackTicksOffset));
		}

		return CustomDifficultySettings.MonsterAttackMultiplier;
	}
}
