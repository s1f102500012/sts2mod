using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

public sealed class MagicMissileRune : HextechRelicBase
{
	internal const int MissileCount = 3;
	internal const decimal MaxHpDamagePercent = 2m;

	private bool _triggeredThisTurn;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Missiles", MissileCount),
		new DynamicVar("MaxHpDamagePercent", MaxHpDamagePercent)
	];

	public override Task BeforeCombatStart()
	{
		ResetTriggered(null);
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTriggered(null);
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetTriggered(combatState);
		}

		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		EnsureTurnScopedStateCurrent(ResetTriggered);
		if (HasTurnProcTriggered(nameof(MagicMissileRune), _triggeredThisTurn)
			|| Owner == null
			|| Owner.Creature.IsDead
			|| !cardPlay.IsFirstInSeries
			|| !IsOwnedAttack(cardPlay.Card))
		{
			return Task.CompletedTask;
		}

		HextechCombatState? combatState = Owner.Creature.CombatState;
		List<Creature> targets = ResolveTargets(cardPlay, combatState);
		if (combatState == null || targets.Count == 0
			|| !TryConsumeTurnProc(nameof(MagicMissileRune), ref _triggeredThisTurn))
		{
			return Task.CompletedTask;
		}

		Flash(targets);
		Creature source = Owner.Creature;
		decimal damagePercent = DynamicVars["MaxHpDamagePercent"].BaseValue;
		if (HextechPlayerContextHelper.IsNetworkMultiplayerRun())
		{
			_ = TaskHelper.RunSafely(PlayVolleyVfxAsync(source, targets));
			return ResolveVolleyDamageInLockstepAsync(choiceContext, source, combatState, targets, damagePercent);
		}

		_ = TaskHelper.RunSafely(ResolveVolleyAfterCardSettlesAsync(
			source,
			combatState,
			cardPlay.Card,
			targets,
			damagePercent));
		return Task.CompletedTask;
	}

	private static async Task PlayVolleyVfxAsync(Creature source, IReadOnlyList<Creature> targets)
	{
		await Task.WhenAll(Enumerable.Range(0, MissileCount)
			.SelectMany(missileIndex => targets
				.Select(target => HextechCombatVfx.PlayMagicMissile(source, target, missileIndex))));
	}

	private static async Task ResolveVolleyDamageInLockstepAsync(
		PlayerChoiceContext choiceContext,
		Creature source,
		HextechCombatState combatState,
		IReadOnlyList<Creature> targets,
		decimal damagePercent)
	{
		for (int missileIndex = 0; missileIndex < MissileCount; missileIndex++)
		{
			if (source.IsDead || !ReferenceEquals(source.CombatState, combatState))
			{
				return;
			}

			foreach (Creature target in targets)
			{
				if (!target.IsAlive || !ReferenceEquals(target.CombatState, combatState))
				{
					continue;
				}

				await HextechGameApiCompat.Damage(
					choiceContext,
					target,
					CalculateMissileDamage(target.MaxHp, damagePercent),
					ValueProp.Unpowered,
					source,
					null);
			}
		}
	}

	private static async Task ResolveVolleyAfterCardSettlesAsync(
		Creature source,
		HextechCombatState combatState,
		CardModel triggeringCard,
		IReadOnlyList<Creature> targets,
		decimal damagePercent)
	{
		if (!await HextechCardPlayTiming.WaitForCardPlayFinishedAsync(source, combatState, triggeringCard))
		{
			return;
		}

		await ResolveVolleyAsync(source, combatState, targets, damagePercent);
	}

	private static async Task ResolveVolleyAsync(
		Creature source,
		HextechCombatState combatState,
		IReadOnlyList<Creature> targets,
		decimal damagePercent)
	{
		// 弹道不能占住 AfterCardPlayed；玩家继续操作时，独立任务仍按命中顺序串行结算伤害命令。
		Task<bool>[][] arrivalTasks = Enumerable.Range(0, MissileCount)
			.Select(missileIndex => targets
				.Select(target => HextechCombatVfx.PlayMagicMissile(source, target, missileIndex))
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
				if (!arrivals[targetIndex] || !target.IsAlive || !ReferenceEquals(target.CombatState, combatState))
				{
					continue;
				}

				int damage = CalculateMissileDamage(target.MaxHp, damagePercent);
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

	internal static int CalculateMissileDamage(decimal targetMaxHp, decimal damagePercent = MaxHpDamagePercent)
	{
		return Math.Max(1, FloorToInt(Math.Max(0m, targetMaxHp) * Math.Max(0m, damagePercent) / 100m));
	}

	private List<Creature> ResolveTargets(CardPlay cardPlay, HextechCombatState? combatState)
	{
		if (combatState == null)
		{
			return [];
		}

		IEnumerable<Creature> targets = cardPlay.Card.TargetType == TargetType.AllEnemies
			? combatState.HittableEnemies
			: cardPlay.Target is { Side: CombatSide.Enemy } target
				? [target]
				: [];
		return targets
			.Where(static target => target.IsAlive && target.Side == CombatSide.Enemy)
			.OrderBy(static target => target.CombatId ?? uint.MaxValue)
			.ToList();
	}

	private void ResetTriggered()
	{
		ResetTriggered(null);
	}

	private void ResetTriggered(HextechCombatState? combatState)
	{
		_triggeredThisTurn = false;
		UpdateTurnScopedStateIdentity(combatState);
	}
}
