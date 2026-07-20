namespace HextechRunes;

public sealed class EnlightenmentRune : HextechRelicBase
{
	// 走原版的 Late 通道:在所有常规费用修改(溢流+1等)之后做最终封顶,
	// 结果与遗物获取顺序无关——开悟+溢流恒为 1(此前按获取顺序可能算成 2,玩家反馈)。
	public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		// originalCost 此时已是常规通道(溢流等)累计后的值。
		modifiedCost = originalCost;
		if (Owner == null
			|| card.Owner != Owner
			|| card.EnergyCost.CostsX
			|| originalCost <= 1m)
		{
			return false;
		}

		modifiedCost = 1m;
		return true;
	}
}
