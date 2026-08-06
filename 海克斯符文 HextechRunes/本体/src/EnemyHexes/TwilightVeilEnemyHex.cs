namespace HextechRunes;

internal sealed class TwilightVeilEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.TwilightVeil;

	internal override async Task AfterBlockGained(HextechEnemyHexContext context, Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
	{
		if (!ShouldMirrorBlock(creature.Side, amount)
			|| creature.CombatState?.RunState != context.RunState)
		{
			return;
		}

		foreach (Creature enemy in context.GetAliveEnemies(creature.CombatState))
		{
			await CreatureCmd.GainBlock(enemy, amount, ValueProp.Unpowered, null, fast: true);
		}
	}

	internal static bool ShouldMirrorBlock(CombatSide side, decimal amount)
	{
		return side == CombatSide.Player && amount > 0m;
	}
}
