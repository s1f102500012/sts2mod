namespace HextechRunes;

public abstract class AttributeConversionRelicBase : HextechRelicBase
{
	private bool _isConverting;
	private decimal? _pendingAmount;
	private Creature? _pendingApplier;
	private CardModel? _pendingCardSource;

	protected abstract bool ShouldConvert(PowerModel canonicalPower);

	protected abstract bool ShouldConvertAppliedPower(PowerModel power);

	protected abstract Task ApplyConvertedPower(decimal amount, Creature? applier, CardModel? cardSource);

	protected abstract Task RevertOriginalPower(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource);

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingAmount = null;
		_pendingApplier = null;
		_pendingCardSource = null;
		_isConverting = false;
		return Task.CompletedTask;
	}

	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = amount;
		if (_isConverting || Owner == null || target != Owner.Creature || amount == 0m || !ShouldConvert(canonicalPower))
		{
			return false;
		}

		// Replace the original stat change with the converted one after the hook pipeline finishes.
		_pendingAmount = amount;
		_pendingApplier = applier;
		_pendingCardSource = null;
		modifiedAmount = 0m;
		return true;
	}

	public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
	{
		if (_pendingAmount is not decimal amount)
		{
			return;
		}

		Creature? applier = _pendingApplier;
		CardModel? cardSource = _pendingCardSource;
		_pendingAmount = null;
		_pendingApplier = null;
		_pendingCardSource = null;

		_isConverting = true;
		try
		{
			Flash();
			await ApplyConvertedPower(amount, applier, cardSource);
		}
		finally
		{
			_isConverting = false;
		}
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_isConverting || Owner == null || amount == 0m || power.Owner != Owner.Creature || !ShouldConvertAppliedPower(power))
		{
			return;
		}

		_isConverting = true;
		try
		{
			Flash();
			await RevertOriginalPower(power, amount, applier, cardSource);
			await ApplyConvertedPower(amount, applier, cardSource);
		}
		finally
		{
			_isConverting = false;
		}
	}
}
