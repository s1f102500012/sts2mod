namespace HextechRunes;

public sealed class TormentorRune : LimitedDebuffProcRelicBase
{
	private bool _applyingBurnProc;

	protected override int MaxProcsPerTurn => 1;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechBurnPower>()
	];

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_applyingBurnProc)
		{
			return;
		}

		await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
	}

	protected override async Task OnEnemyDebuffApplied(Creature target)
	{
		try
		{
			_applyingBurnProc = true;
			await PowerCmd.Apply<HextechBurnPower>(target, 4m, Owner!.Creature, null);
		}
		finally
		{
			_applyingBurnProc = false;
		}
	}
}
