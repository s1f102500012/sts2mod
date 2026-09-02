namespace HextechRunes;

public sealed class OminousPactRune : HextechRelicBase
{
	private int _summoningFromDoomDepth;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<DoomPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return HandleDoomApplied(choiceContext, power, amount, applier);
	}

	private async Task HandleDoomApplied(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| power is not DoomPower
			|| power.Owner?.Side != CombatSide.Enemy
			|| applier != Owner.Creature
			|| amount <= 0m
			|| _summoningFromDoomDepth > 0)
		{
			return;
		}

		Flash(power.Owner == null ? Array.Empty<Creature>() : [power.Owner]);
		_summoningFromDoomDepth++;
		try
		{
			await OstyCmd.Summon(choiceContext, Owner, amount, this);
		}
		finally
		{
			_summoningFromDoomDepth--;
		}
	}
}
