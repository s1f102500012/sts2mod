namespace HextechRunes;

public sealed class TerminalIllnessRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PoisonPower>()
	];

	public override bool IsAvailableForPlayer(Player player) => IsSilentPlayer(player);

	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = amount;
		if (Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| canonicalPower is not PoisonPower
			|| amount != -1m
			|| applier != null)
		{
			return false;
		}

		modifiedAmount = 0m;
		return true;
	}

	public override Task AfterModifyingPowerAmountReceived(PowerModel power)
	{
		Flash();
		return Task.CompletedTask;
	}
}
