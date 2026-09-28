namespace HextechRunes;

public sealed class HubrisRune : HextechRelicBase, IHextechSharedCombatVictoryRune
{
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStacks
	{
		get => _stacks;
		set
		{
			_stacks = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _stacks : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("StacksPerBonus", 3m),
		new PowerVar<StrengthPower>(1m),
		new CardsVar(1)
	];

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (IsNetworkMultiplayer())
		{
			return Task.CompletedTask;
		}

		return ApplySharedCombatVictory(room);
	}

	public Task ApplySharedCombatVictory(CombatRoom room)
	{
		if (Owner != null && !Owner.Creature.IsDead)
		{
			SavedStacks++;
			Flash(Array.Empty<Creature>());
		}

		return Task.CompletedTask;
	}

	public override async Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return;
		}

		int bonus = GetBonusAmount();
		if (bonus <= 0)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, bonus * DynamicVars.Strength.BaseValue, Owner.Creature, null);
	}

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner || !IsOwnersFirstTurn)
		{
			return count;
		}

		return count + GetBonusAmount() * DynamicVars.Cards.BaseValue;
	}

	private int GetBonusAmount()
	{
		return FloorToInt(_stacks / DynamicVars["StacksPerBonus"].BaseValue);
	}
}
