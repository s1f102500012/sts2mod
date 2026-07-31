namespace HextechRunes;

public sealed class StartupRoutineRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new BlockVar(16m, ValueProp.Unpowered)
	];

	public override async Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
	}
}
