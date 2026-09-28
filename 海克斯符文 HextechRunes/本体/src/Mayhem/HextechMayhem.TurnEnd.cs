namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override async Task BeforeTurnEndForParticipants(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		CombatRoom? combatRoom = RunState.CurrentRoom as CombatRoom;
		IReadOnlyList<Creature> participantList = participants.ToList();

		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			participantList,
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
