namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
	{
		return HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterBlockGained(context, creature, amount, props, cardSource));
	}
}
