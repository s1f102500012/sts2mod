namespace HextechRunes;

public sealed class HappyAccidentRune : HextechRelicBase
{
	private int _statusOrbsThisCombat;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbCount", 1m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner
			|| Owner == null
			|| Owner.Creature.IsDead
			|| Owner.Creature.CombatState == null)
		{
			return;
		}

		int statusCount = CountStatusCards(Owner.PlayerCombatState?.ExhaustPile.Cards ?? []);
		int orbCount = ResolveOrbCount(statusCount, DynamicVars["OrbCount"].IntValue);
		if (orbCount <= 0)
		{
			return;
		}

		Flash();
		for (int i = 0; i < orbCount; i++)
		{
			int orbOrdinal = ConsumeCombatProcOrdinal(nameof(HappyAccidentRune), ref _statusOrbsThisCombat);
			OrbModel orb = HextechStableRandom.CreateOrb(
				(RunState)Owner.RunState,
				Owner,
				"happy-accident-exhaust-status-orb",
				orbOrdinal,
				Owner.Creature.CombatState.RoundNumber);
			await OrbCmd.Channel(choiceContext, orb, Owner);
		}
	}

	internal static int CountStatusCards(IEnumerable<CardModel> cards)
	{
		return cards.Count(static card => card.Type == CardType.Status);
	}

	internal static int ResolveOrbCount(int statusCount, int orbsPerStatus)
	{
		return Math.Max(0, statusCount) * Math.Max(0, orbsPerStatus);
	}
}
