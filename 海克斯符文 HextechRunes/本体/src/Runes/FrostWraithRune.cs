namespace HextechRunes;

public sealed class FrostWraithRune : HextechRelicBase
{
	internal const int TurnsNeeded = 2;
	internal const int TemporarySlowAmount = 50;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("TurnsNeeded", TurnsNeeded),
		new PowerVar<HextechPlayerSlowPower>("SlowPower", TemporarySlowAmount)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechPlayerSlowPower>()
	];

	// 额外回合不推进 RoundNumber 且回合开始 hook 会重入,周期触发按 RoundNumber 防重。
	private int _lastProcRound = -1;

	public override Task BeforeCombatStart()
	{
		_lastProcRound = -1;
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner
			|| Owner.Creature.IsDead
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| !ShouldTriggerForRound(combatState.RoundNumber, DynamicVars["TurnsNeeded"].IntValue)
			|| _lastProcRound == combatState.RoundNumber)
		{
			return;
		}

		_lastProcRound = combatState.RoundNumber;
		await ApplySlow(combatState);
	}

	internal static bool ShouldTriggerForRound(int roundNumber, int turnsNeeded)
	{
		return roundNumber > 1
			&& turnsNeeded > 0
			&& (roundNumber - 1) % turnsNeeded == 0;
	}

	private async Task ApplySlow(HextechCombatState combatState)
	{
		IReadOnlyList<Creature> enemies = combatState.HittableEnemies.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		await PowerCmd.Apply<HextechTemporarySlowPower>(enemies, DynamicVars["SlowPower"].BaseValue, Owner.Creature, null);
	}
}
