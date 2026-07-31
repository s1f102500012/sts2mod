namespace HextechRunes;

/// <summary>
/// 升级：永恒铠甲——打出永恒铠甲后,本场战斗内覆甲在回合开始时不再减少。
/// 以"本场打出过永恒铠甲"为开关，将覆甲的负向变化归零。
/// </summary>
public sealed class EternalArmorUpgradeRune : CardUpgradeRuneBase<EternalArmor>
{
	private bool _activeThisCombat;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<EternalArmor>(),
		HoverTipFactory.FromPower<PlatingPower>()
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return true;
	}

	public override Task BeforeCombatStart()
	{
		_activeThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_activeThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!_activeThisCombat && cardPlay.Card is EternalArmor && cardPlay.Card.Owner == Owner)
		{
			_activeThisCombat = true;
			Flash();
		}

		return Task.CompletedTask;
	}

	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = amount;
		if (!_activeThisCombat
			|| Owner == null
			|| target != Owner.Creature
			|| canonicalPower is not PlatingPower
			|| amount >= 0m)
		{
			return false;
		}

		modifiedAmount = 0m;
		Flash();
		return true;
	}
}
