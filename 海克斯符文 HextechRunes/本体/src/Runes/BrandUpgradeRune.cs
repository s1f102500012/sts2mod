namespace HextechRunes;

public sealed class BrandUpgradeRune : CardUpgradeRuneBase<Brand>
{
	internal const int DamagePercentPerBrand = 3;

	private int _brandPlays;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCounter
	{
		get => _brandPlays;
		set
		{
			_brandPlays = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _brandPlays : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamagePercentPerBrand", DamagePercentPerBrand)
	];

	protected override bool IsAvailableForCharacter(Player player) => IsIroncladPlayer(player);

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card is not Brand)
		{
			return Task.CompletedTask;
		}

		SavedCounter++;
		Flash();
		return Task.CompletedTask;
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (!IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource))
		{
			return 1m;
		}

		return CalculateDamageMultiplier(_brandPlays, DynamicVars["DamagePercentPerBrand"].BaseValue);
	}

	internal static decimal CalculateDamageMultiplier(int brandPlays, decimal percentPerPlay)
	{
		return 1m + Math.Max(0, brandPlays) * percentPerPlay / 100m;
	}
}
