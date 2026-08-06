namespace HextechRunes;

public sealed class ThornmailRune : HextechRelicBase
{
	internal const int MaxHpPerThorns = 20;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("MaxHpPerThorns", MaxHpPerThorns),
		new PowerVar<ThornsPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ThornsPower>()
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		int thorns = CalculateThorns(Owner.Creature.MaxHp, DynamicVars["MaxHpPerThorns"].BaseValue);
		if (thorns <= 0)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<ThornsPower>(Owner.Creature, thorns, Owner.Creature, null);
	}

	internal static int CalculateThorns(decimal maxHp, decimal maxHpPerThorns = MaxHpPerThorns)
	{
		return maxHpPerThorns <= 0m ? 0 : Math.Max(0, FloorToInt(maxHp / maxHpPerThorns));
	}
}
