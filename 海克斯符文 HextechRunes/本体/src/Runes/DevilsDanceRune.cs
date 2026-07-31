namespace HextechRunes;

public sealed class DevilsDanceRune : HextechRelicBase
{
	private int _attacksPlayedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedAttacksPlayedThisCombat
	{
		get => IsNetworkMultiplayer() ? 0 : _attacksPlayedThisCombat;
		set => _attacksPlayedThisCombat = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("AttacksPerMaxHp", 3m),
		new MaxHpVar(1m)
	];

	public override Task BeforeCombatStart()
	{
		_attacksPlayedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_attacksPlayedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedAttack(cardPlay.Card))
		{
			return;
		}

		if (ShouldUseNetworkCombatHistory())
		{
			await ResolveAttackProgressFromHistory();
			return;
		}

		int previousAttacksPlayed = _attacksPlayedThisCombat;
		_attacksPlayedThisCombat++;
		if (CountMaxHpTriggers(
				previousAttacksPlayed,
				_attacksPlayedThisCombat,
				DynamicVars["AttacksPerMaxHp"].IntValue) > 0)
		{
			await GainMaxHpForAttackThreshold();
		}
	}

	public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (ShouldUseNetworkCombatHistory() && IsOwnedAttack(cardPlay.Card))
		{
			await ResolveAttackProgressFromHistory();
		}
	}

	private async Task ResolveAttackProgressFromHistory()
	{
		int attacksPlayed = CountOwnedAttackCardsPlayedFromHistory(firstInSeriesOnly: false, includeAutoPlay: true);
		int previousAttacksPlayed = _attacksPlayedThisCombat;
		if (attacksPlayed <= previousAttacksPlayed)
		{
			return;
		}

		_attacksPlayedThisCombat = attacksPlayed;
		int triggers = CountMaxHpTriggers(
			previousAttacksPlayed,
			attacksPlayed,
			DynamicVars["AttacksPerMaxHp"].IntValue);
		for (int i = 0; i < triggers; i++)
		{
			await GainMaxHpForAttackThreshold();
		}
	}

	private async Task GainMaxHpForAttackThreshold()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars.MaxHp.BaseValue);
	}

	internal static int CountMaxHpTriggers(int previousAttacksPlayed, int attacksPlayed, int attacksPerTrigger)
	{
		int threshold = Math.Max(1, attacksPerTrigger);
		return Math.Max(0, attacksPlayed / threshold - Math.Max(0, previousAttacksPlayed) / threshold);
	}
}
