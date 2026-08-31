namespace HextechRunes;

internal sealed class IInspectEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.IInspect;

	internal override bool ShouldDraw(HextechEnemyHexContext context, Player player, bool fromHandDraw)
	{
		if (player.Creature.Side != CombatSide.Player
			|| player.Creature.IsDead
			|| player.Creature.CombatState?.RunState != context.RunState)
		{
			return true;
		}

		int limit = context.TierValue(Kind, 0, 1, 2);
		return !TryPreventExtraDraw(context.Tracking, player.NetId, limit, fromHandDraw);
	}

	internal static bool TryPreventExtraDraw(
		HextechMayhemCombatTrackingState tracking,
		ulong playerId,
		int limit,
		bool fromHandDraw)
	{
		if (fromHandDraw || tracking.IsPlayerTurnStart(playerId) || limit <= 0)
		{
			return false;
		}

		int prevented = tracking.InspectExtraDrawsPreventedThisTurn.GetValueOrDefault(playerId);
		if (prevented >= limit)
		{
			return false;
		}

		tracking.InspectExtraDrawsPreventedThisTurn[playerId] = prevented + 1;
		return true;
	}
}
