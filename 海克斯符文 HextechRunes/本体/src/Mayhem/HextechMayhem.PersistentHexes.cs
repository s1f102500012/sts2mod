using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private async Task ApplyPersistentMonsterHexes(Creature creature, bool replayOneShotPowers = false)
	{
		int? maxHpBaseOverride = replayOneShotPowers ? creature.MaxHp : null;
		_ = CaptureMonsterMaxHpCoefficientBase(
			creature,
			maxHpBaseOverride,
			out bool migratedLegacyCoefficients);
		if (migratedLegacyCoefficients)
		{
			// 旧存档的 persistent markers 已经置位，后续各 effect 会跳过 Apply。
			// 基准迁移后必须在这里统一投影一次，否则旧实际 MaxHp 会与新基准永久脱节。
			await ReapplyMonsterMaxHpCoefficients(creature);
		}

		await HextechEnemyHexDispatcher.ForEachActiveOrdered(
			this,
			static effect => effect.PersistentOrder,
			(effect, context) => effect.ApplyPersistentToEnemy(context, creature, maxHpBaseOverride, replayOneShotPowers));
	}

	internal int CaptureMonsterMaxHpCoefficientBase(Creature creature, int? baseMaxHpOverride = null)
	{
		return CaptureMonsterMaxHpCoefficientBase(
			creature,
			baseMaxHpOverride,
			out _);
	}

	private int CaptureMonsterMaxHpCoefficientBase(
		Creature creature,
		int? baseMaxHpOverride,
		out bool migratedLegacyCoefficients)
	{
		migratedLegacyCoefficients = false;
		if (creature.CombatId is not uint combatId)
		{
			return Math.Max(1, baseMaxHpOverride ?? creature.MaxHp);
		}

		if (baseMaxHpOverride is int overriddenBase)
		{
			int normalizedOverride = Math.Max(1, overriddenBase);
			_combatTracking.MonsterMaxHpCoefficientBase[combatId] = normalizedOverride;
			_combatTracking.MonsterMaxHpCoefficientProjected.Remove(combatId);
			return normalizedOverride;
		}

		if (_combatTracking.MonsterMaxHpCoefficientBase.TryGetValue(combatId, out int trackedBase)
			&& trackedBase > 0)
		{
			migratedLegacyCoefficients =
				_combatTracking.MonsterMaxHpCoefficientProjected.GetValueOrDefault(combatId, 0) <= 0
				&& HasAppliedMonsterMaxHpCoefficientMarker(combatId);
			return trackedBase;
		}

		bool coefficientsWereAlreadyApplied = HasAppliedMonsterMaxHpCoefficientMarker(combatId);
		int baseMaxHp = coefficientsWereAlreadyApplied
			? ResolveLegacyMonsterMaxHpCoefficientBase(creature, combatId)
			: Math.Max(1, creature.MaxHp);
		migratedLegacyCoefficients = coefficientsWereAlreadyApplied;

		_combatTracking.MonsterMaxHpCoefficientBase[combatId] = baseMaxHp;
		return baseMaxHp;
	}

	private bool HasAppliedMonsterMaxHpCoefficientMarker(uint combatId)
	{
		return
			_combatTracking.GoliathApplied.Contains(combatId)
			|| _combatTracking.AstralBodyApplied.Contains(combatId)
			|| _combatTracking.GoldenSpatulaApplied.Contains(combatId)
			|| _combatTracking.StatsApplied.Contains(combatId)
			|| _combatTracking.StatsOnStatsApplied.Contains(combatId)
			|| _combatTracking.StatsOnStatsOnStatsApplied.Contains(combatId)
			|| _combatTracking.MadScientistApplied.Contains(combatId)
			|| _combatTracking.TankEngineStacks.GetValueOrDefault(combatId, 0) > 0;
	}

	private int ResolveLegacyMonsterMaxHpCoefficientBase(Creature creature, uint combatId)
	{
		HextechEnemyHexContext context = new(this);
		List<decimal> appliedFixedBonusFractions = new(3);
		if (_combatTracking.GoliathApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(context.TierValue(MonsterHexKind.Goliath, 0.20m, 0.30m, 0.40m));
		}

		if (_combatTracking.AstralBodyApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(context.TierValue(MonsterHexKind.AstralBody, 0.20m, 0.30m, 0.40m));
		}

		if (_combatTracking.GoldenSpatulaApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(context.TierValue(MonsterHexKind.GoldenSpatula, 0.25m, 0.30m, 0.45m));
		}

		if (_combatTracking.StatsApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.Stats, context.GetStrengthTier(MonsterHexKind.Stats)));
		}

		if (_combatTracking.StatsOnStatsApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStats, context.GetStrengthTier(MonsterHexKind.StatsOnStats)));
		}

		if (_combatTracking.StatsOnStatsOnStatsApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStatsOnStats, context.GetStrengthTier(MonsterHexKind.StatsOnStatsOnStats)));
		}

		decimal madScientistLossFraction = _combatTracking.MadScientistApplied.Contains(combatId)
			? context.TierValue(MonsterHexKind.MadScientist, 0.30m, 0.15m, 0.00m)
			: 0m;
		int tankEngineStacks = Math.Max(0, _combatTracking.TankEngineStacks.GetValueOrDefault(combatId, 0));
		int? rawMonsterMaxHp = creature.MonsterMaxHpBeforeModification is int rawMaxHp && rawMaxHp > 0
			? rawMaxHp
			: null;
		int migratedBaseMaxHp = HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
			creature.MaxHp,
			rawMonsterMaxHp,
			appliedFixedBonusFractions,
			madScientistLossFraction,
			tankEngineStacks);
		HextechLog.Info(
			$"[{ModInfo.Id}][Mayhem] Migrated legacy enemy max HP base: combatId={combatId} current={creature.MaxHp} raw={rawMonsterMaxHp?.ToString() ?? "unknown"} fixedBonuses={string.Join(",", appliedFixedBonusFractions)} madLoss={madScientistLossFraction} tankStacks={tankEngineStacks} base={migratedBaseMaxHp}");
		return migratedBaseMaxHp;
	}

	internal async Task ReapplyMonsterMaxHpCoefficients(Creature creature, int? baseMaxHpOverride = null)
	{
		int baseMaxHp = CaptureMonsterMaxHpCoefficientBase(creature, baseMaxHpOverride);
		if (baseMaxHpOverride == null)
		{
			baseMaxHp = ReconcileObservedMonsterMaxHpChange(creature, baseMaxHp);
		}

		decimal scale = GetMonsterMaxHpCoefficientScale(creature);
		int expectedMaxHp = (int)Math.Clamp(Math.Floor(baseMaxHp * scale), 1m, int.MaxValue);
		int delta = expectedMaxHp - creature.MaxHp;
		if (delta > 0)
		{
			await GainMonsterMaxHpWithoutHeal(creature, delta);
			TrackProjectedMonsterMaxHp(creature);
			return;
		}

		if (delta < 0)
		{
			await CreatureCmdCompat.SetMaxHp(creature, expectedMaxHp);
		}

		TrackProjectedMonsterMaxHp(creature);
		await KeepFurCoatMarkedEnemyAtOneHp(creature);
	}

	private int ReconcileObservedMonsterMaxHpChange(Creature creature, int baseMaxHp)
	{
		if (creature.CombatId is not uint combatId
			|| !_combatTracking.MonsterMaxHpCoefficientProjected.TryGetValue(
				combatId,
				out int projectedMaxHp)
			|| projectedMaxHp <= 0
			|| projectedMaxHp == creature.MaxHp)
		{
			return baseMaxHp;
		}

		long observedDelta = (long)creature.MaxHp - projectedMaxHp;
		int adjustedBaseMaxHp = (int)Math.Clamp(
			(long)baseMaxHp + observedDelta,
			1L,
			int.MaxValue);
		_combatTracking.MonsterMaxHpCoefficientBase[combatId] = adjustedBaseMaxHp;
		HextechLog.Info(
			$"[{ModInfo.Id}][Mayhem] Reconciled enemy max HP base after an external change: "
			+ $"combatId={combatId} base={baseMaxHp} projected={projectedMaxHp} "
			+ $"observed={creature.MaxHp} adjustedBase={adjustedBaseMaxHp}");
		return adjustedBaseMaxHp;
	}

	private void TrackProjectedMonsterMaxHp(Creature creature)
	{
		if (creature.CombatId is uint combatId)
		{
			_combatTracking.MonsterMaxHpCoefficientProjected[combatId] =
				Math.Max(1, creature.MaxHp);
		}
	}

	private decimal GetMonsterMaxHpCoefficientScale(Creature creature)
	{
		HextechEnemyHexContext context = new(this);
		return HextechEnemyCoefficientHelper.CombineBonusFractionsByHex(
			HextechEnemyHexEffects.GetActive(this)
				.OfType<IHextechEnemyMaxHpCoefficientProvider>()
				.Select(provider =>
				(((HextechEnemyHexEffect)provider).Kind, provider.GetMaxHpBonusFraction(context, creature))));
	}

	internal static async Task GainMonsterMaxHpWithoutHeal(Creature creature, int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		int oldMaxHp = creature.MaxHp;
		int oldCurrentHp = creature.CurrentHp;
		await CreatureCmdCompat.SetMaxHp(creature, oldMaxHp + amount);

		int actualMaxHpGain = Math.Max(0, creature.MaxHp - oldMaxHp);
		if (actualMaxHpGain <= 0)
		{
			return;
		}

		int newCurrentHp = IsFurCoatMarkedEnemy(creature)
			? 1
			: Math.Min(creature.MaxHp, oldCurrentHp + actualMaxHpGain);
		if (newCurrentHp != creature.CurrentHp)
		{
			await CreatureCmd.SetCurrentHp(creature, newCurrentHp);
		}
	}

	private static Task KeepFurCoatMarkedEnemyAtOneHp(Creature creature)
	{
		if (IsFurCoatMarkedEnemy(creature) && creature.CurrentHp != 1)
		{
			return CreatureCmd.SetCurrentHp(creature, 1m);
		}

		return Task.CompletedTask;
	}

	private static bool IsFurCoatMarkedEnemy(Creature creature)
	{
		if (creature.Side != CombatSide.Enemy || !creature.IsAlive || creature.CombatState == null)
		{
			return false;
		}

		foreach (RelicModel relic in creature.CombatState.Players.SelectMany(static player => player.Relics))
		{
			if (relic is not FurCoat furCoat || furCoat.Owner?.RunState.CurrentMapPoint == null)
			{
				continue;
			}

			if (furCoat.GetMarkedCoords()?.Contains(furCoat.Owner.RunState.CurrentMapPoint.coord) == true)
			{
				return true;
			}
		}

		return false;
	}

	internal void UpdateEnemyScale(Creature creature)
	{
		float baseScale = HasActiveMonsterHex(MonsterHexKind.Goliath) ? 1.35f : 1f;
		// 巨人杀手敌方版让敌人体型缩小(纯视觉,呼应「体型变小」的设定,无机制意义)。
		float giantSlayerShrink = HasActiveMonsterHex(MonsterHexKind.GiantSlayer) ? 0.25f : 0f;
		int tankStacks = creature.CombatId == null ? 0 : _combatTracking.TankEngineStacks.GetValueOrDefault(creature.CombatId.Value, 0);
		int shrinkStacks = creature.CombatId == null ? 0 : _combatTracking.ShrinkEngineStacks.GetValueOrDefault(creature.CombatId.Value, 0);
		float finalScale = Math.Max(0.2f, baseScale + tankStacks * 0.05f - shrinkStacks * 0.02f - giantSlayerShrink);
		NCombatRoom.Instance?.GetCreatureNode(creature)?.SetDefaultScaleTo(finalScale, 0f);
	}
}
