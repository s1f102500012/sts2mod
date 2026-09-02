namespace HextechRunes;

public sealed class SpinToWinRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<DrawCardsNextTurnPower>(),
		HoverTipFactory.FromPower<EnergyNextTurnPower>(),
		HoverTipFactory.FromPower<SummonNextTurnPower>(),
		HoverTipFactory.FromPower<StarNextTurnPower>()
	];

	public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return ConvertDelayedResource(choiceContext, power, amount);
	}

	internal static bool IsConvertiblePower(PowerModel power)
	{
		return power is DrawCardsNextTurnPower
			or EnergyNextTurnPower
			or SummonNextTurnPower
			or StarNextTurnPower;
	}

	private async Task ConvertDelayedResource(PlayerChoiceContext choiceContext, PowerModel power, decimal amount)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| power.Owner != Owner.Creature
			|| amount <= 0m
			|| power.Amount <= 0
			|| !IsConvertiblePower(power))
		{
			return;
		}

		decimal pendingAmount = power.Amount;
		switch (power)
		{
			case DrawCardsNextTurnPower:
				await CardPileCmd.Draw(choiceContext, pendingAmount, Owner, fromHandDraw: false);
				break;
			case EnergyNextTurnPower:
				await PlayerCmd.GainEnergy(pendingAmount, Owner);
				break;
			case SummonNextTurnPower:
				await OstyCmd.Summon(choiceContext, Owner, pendingAmount, power);
				break;
			case StarNextTurnPower:
				await PlayerCmd.GainStars(pendingAmount, Owner);
				break;
		}

		await PowerCmd.Remove(power);
		Flash();
	}
}
