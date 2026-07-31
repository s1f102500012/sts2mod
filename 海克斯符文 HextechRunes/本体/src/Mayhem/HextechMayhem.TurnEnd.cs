namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		CombatRoom? combatRoom = RunState.CurrentRoom as CombatRoom;

		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.BeforeTurnEnd(context, choiceContext, side, combatRoom));

		if (side == CombatSide.Player)
		{
			_combatTracking.PreparePlayerSideTurnEnd();
			if (combatRoom != null)
			{
				RefreshPlayerAttackCostDoublingPreviews(HextechCombatCreatureHelper.GetAlivePlayerSideCreatures(combatRoom.CombatState));
			}
		}
	}
}
