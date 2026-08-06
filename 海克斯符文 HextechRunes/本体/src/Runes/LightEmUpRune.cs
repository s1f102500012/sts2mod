using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

public sealed class LightEmUpRune : HextechRelicBase
{
	internal const int AttacksPerVolley = 4;
	internal const int MissileCount = 5;

	private int _attacksPlayedThisCombat;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Attacks", AttacksPerVolley),
		new DynamicVar("Missiles", MissileCount)
	];

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedAttacksPlayedThisCombat
	{
		get => _attacksPlayedThisCombat;
		set
		{
			_attacksPlayedThisCombat = Math.Clamp(value, 0, AttacksPerVolley);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount
	{
		get
		{
			if (IsCanonical)
			{
				return 0;
			}

			return _attacksPlayedThisCombat;
		}
	}

	public override Task BeforeCombatStart()
	{
		ResetAttacksPlayedThisCombat();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetAttacksPlayedThisCombat();
		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!IsCountedAttackPlay(cardPlay))
		{
			return Task.CompletedTask;
		}

		decimal damage = ResolveMissileDamage(HextechCombatHooks.GetEnergyCostForCurrentCardPlay(cardPlay.Card));
		_attacksPlayedThisCombat = AdvanceAttackProgress(
			_attacksPlayedThisCombat,
			damage,
			out bool shouldLaunchVolley);
		InvokeDisplayAmountChanged();
		if (!shouldLaunchVolley
			|| Owner == null
			|| Owner.Creature.IsDead
			|| Owner.Creature.CombatState is not HextechCombatState combatState)
		{
			return Task.CompletedTask;
		}

		List<Creature> targets = ResolveTargets(cardPlay, combatState);
		if (targets.Count == 0)
		{
			return Task.CompletedTask;
		}

		Flash(targets);
		Creature source = Owner.Creature;
		_ = TaskHelper.RunSafely(ResolveVolleyAfterCardSettlesAsync(
			source,
			combatState,
			cardPlay.Card,
			targets,
			damage));
		return Task.CompletedTask;
	}

	private static async Task ResolveVolleyAfterCardSettlesAsync(
		Creature source,
		HextechCombatState combatState,
		CardModel triggeringCard,
		IReadOnlyList<Creature> targets,
		decimal damage)
	{
		if (!await HextechCardPlayTiming.WaitForCardPlayFinishedAsync(source, combatState, triggeringCard))
		{
			return;
		}

		Task<bool>[][] arrivalTasks = Enumerable.Range(0, MissileCount)
			.Select(missileIndex => targets
				.Select(target => HextechCombatVfx.PlayTwinFlamesMissile(source, target, missileIndex))
				.ToArray())
			.ToArray();
		PlayerChoiceContext damageContext = new BlockingPlayerChoiceContext();

		for (int missileIndex = 0; missileIndex < MissileCount; missileIndex++)
		{
			bool[] arrivals = await Task.WhenAll(arrivalTasks[missileIndex]);
			if (source.IsDead || !ReferenceEquals(source.CombatState, combatState))
			{
				return;
			}

			for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
			{
				Creature target = targets[targetIndex];
				if (!arrivals[targetIndex]
					|| !target.IsAlive
					|| !ReferenceEquals(target.CombatState, combatState))
				{
					continue;
				}

				await HextechGameApiCompat.Damage(
					damageContext,
					target,
					damage,
					ValueProp.Unpowered,
					source,
					null);
			}
		}
	}

	private void ResetAttacksPlayedThisCombat()
	{
		_attacksPlayedThisCombat = 0;
		InvokeDisplayAmountChanged();
	}

	internal static int AdvanceAttackProgress(int currentProgress, decimal energyCost, out bool shouldLaunchVolley)
	{
		int progress = Math.Clamp(currentProgress, 0, AttacksPerVolley);
		if (progress < AttacksPerVolley)
		{
			progress++;
		}

		shouldLaunchVolley = progress == AttacksPerVolley && energyCost > 0m;
		return shouldLaunchVolley ? 0 : progress;
	}

	internal static decimal ResolveMissileDamage(decimal energyCost)
	{
		return Math.Max(0m, energyCost);
	}

	private static List<Creature> ResolveTargets(CardPlay cardPlay, HextechCombatState combatState)
	{
		IEnumerable<Creature> targets = cardPlay.Card.TargetType == TargetType.AllEnemies
			? combatState.HittableEnemies
			: cardPlay.Target is { Side: CombatSide.Enemy } target
				? [target]
				: [];
		return targets
			.Where(static target => target.IsAlive && target.Side == CombatSide.Enemy)
			.ToList();
	}

	private bool IsCountedAttackPlay(CardPlay cardPlay)
	{
		return cardPlay.IsFirstInSeries
			&& !cardPlay.IsAutoPlay
			&& IsOwnedAttack(cardPlay.Card);
	}
}
