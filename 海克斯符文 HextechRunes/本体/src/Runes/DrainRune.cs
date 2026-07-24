namespace HextechRunes;

public sealed class DrainRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DoomMultiplier", 2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<DoomPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterSummon(PlayerChoiceContext choiceContext, Player summoner, decimal amount)
	{
		if (summoner != Owner || Owner == null || Owner.Creature.IsDead || amount <= 0m || Owner.Creature.CombatState == null)
		{
			return;
		}

		IReadOnlyList<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Creature target = enemies[FindHighestCurrentHpIndex(enemies.Select(static enemy => enemy.CurrentHp).ToArray())];
		Flash([target]);
		await PowerCmd.Apply<DoomPower>(target, amount * DynamicVars["DoomMultiplier"].BaseValue, Owner.Creature, null);
	}

	internal static int FindHighestCurrentHpIndex(IReadOnlyList<int> currentHpValues)
	{
		if (currentHpValues.Count == 0)
		{
			throw new ArgumentException("At least one current HP value is required.", nameof(currentHpValues));
		}

		int highestIndex = 0;
		for (int i = 1; i < currentHpValues.Count; i++)
		{
			if (currentHpValues[i] > currentHpValues[highestIndex])
			{
				highestIndex = i;
			}
		}

		return highestIndex;
	}
}
