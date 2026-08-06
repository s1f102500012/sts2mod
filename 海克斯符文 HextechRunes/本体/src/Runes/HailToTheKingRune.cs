namespace HextechRunes;

public sealed class HailToTheKingRune : InitialForgeGrantRune, IHextechSharedCombatVictoryRune
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("InitialForgeCount", 2m),
		new DynamicVar("EliteForgeCount", 1m),
		new DynamicVar("BossForgeCount", 1m)
	];

	protected override int InitialForgeCount => DynamicVars["InitialForgeCount"].IntValue;

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
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		if (room.RoomType == RoomType.Elite)
		{
			Flash(Array.Empty<Creature>());
			for (int i = 0; i < DynamicVars["EliteForgeCount"].IntValue; i++)
			{
				HextechForgeGrantHelper.AddRandomForgeReward(Owner, room, HextechRarityTier.Gold);
			}
		}
		else if (room.RoomType == RoomType.Boss)
		{
			Flash(Array.Empty<Creature>());
			for (int i = 0; i < DynamicVars["BossForgeCount"].IntValue; i++)
			{
				HextechForgeGrantHelper.AddRandomForgeReward(Owner, room, HextechRarityTier.Prismatic);
			}
		}

		return Task.CompletedTask;
	}
}
