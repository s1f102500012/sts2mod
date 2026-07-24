namespace HextechRunes;

public abstract class HextechForgeBase : HextechRelicBase
{

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStackCount
	{
		get => StackCount;
		set
		{
			int target = Math.Max(1, value);
			while (StackCount < target)
			{
				IncrementStackCount();
			}

			InvokeDisplayAmountChanged();
		}
	}

	public override bool IsStackable => true;

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? StackCount : 0;

	protected int StackAmount => Math.Max(1, StackCount);

	protected decimal StackMultiplier => StackAmount;

	protected decimal Stacked(decimal value)
	{
		return value * StackMultiplier;
	}

	protected decimal StackedMultiplier(decimal value)
	{
		return 1m + (value - 1m) * StackAmount;
	}

	public void AddForgeStack(bool flash = true)
	{
		IncrementStackCount();
		InvokeDisplayAmountChanged();
		if (flash)
		{
			Flash();
		}
	}
}
