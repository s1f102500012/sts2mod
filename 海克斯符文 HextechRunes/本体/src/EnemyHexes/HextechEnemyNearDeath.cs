using System.Runtime.CompilerServices;

namespace HextechRunes;

/// <summary>
/// 敌方「濒死狂宴」的濒死状态机,镜像玩家版 <see cref="NearDeathFeastRune"/> 的静态判定 API:
/// 敌人生命低于 1 时进入濒死(负血),无法获得治疗与格挡,每 1 点负血获得 1 层力量;
/// 负血达到最大生命的 5%/10%/15%(白银/黄金/棱彩)时真正死亡。
/// 状态按 CombatId 存在 Mayhem 的 CombatTracking 里(联机同步、rejoin/存档随快照走),
/// 两端由同一命令流驱动,结果确定。
/// 死亡联动:负血打满后走标准 SetCurrentHpInternal(0)+killed 死亡链,实验体转阶段、
/// 千足虫接续等"死亡时不移除"的机制照常接管;新阶段/接续体把 HP 设回正值时,
/// CurrentHp setter 分支会清掉残留的濒死状态(见 ClearIfRecovered)。
/// </summary>
internal static class HextechEnemyNearDeath
{
	private const decimal LimitPercentPerTier = 0.05m;
	private static readonly ConditionalWeakTable<Creature, SemaphoreSlim> StrengthSyncGates = new();

	/// <summary>敌人是否具备濒死狂宴(本场海克斯激活且可参与追踪)。</summary>
	internal static bool HasDyingState(Creature creature)
	{
		return TryGetContext(creature, out _, out _);
	}

	internal static bool IsDyingButAlive(Creature creature)
	{
		if (!TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId))
		{
			return false;
		}

		return modifier.CombatTracking.NearDeathFeastEnemyDebt.TryGetValue(combatId, out int debt)
			&& creature.CurrentHp > 0
			&& debt < GetDeathNegativeHpLimit(creature, modifier);
	}

	internal static bool ShouldPreventSustain(Creature creature)
	{
		return IsDyingButAlive(creature);
	}

	internal static bool ShouldInterceptLoseHp(Creature creature, decimal amount)
	{
		if (amount <= 0m || !TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId))
		{
			return false;
		}

		int hpLoss = (int)Math.Min(amount, 999999999m);
		return modifier.CombatTracking.NearDeathFeastEnemyDebt.ContainsKey(combatId)
			|| creature.CurrentHp - hpLoss < 1;
	}

	internal static DamageResult LoseHpAllowingDying(Creature creature, decimal amount, ValueProp props)
	{
		if (amount <= 0m || !TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId))
		{
			return NearDeathFeastRune.CreateDamageResult(creature, props, 0, false, 0);
		}

		HextechMayhemCombatTrackingState tracking = modifier.CombatTracking;
		bool wasDying = tracking.NearDeathFeastEnemyDebt.TryGetValue(combatId, out int debt);
		int oldEffectiveHp = wasDying ? -debt : creature.CurrentHp;
		int hpLoss = (int)Math.Min(amount, 999999999m);
		int newEffectiveHp = oldEffectiveHp - hpLoss;
		int deathLimit = GetDeathNegativeHpLimit(creature, modifier);

		if (newEffectiveHp <= -deathLimit)
		{
			// 负血打满:退出濒死、走标准死亡链(转阶段/接续/掉魂等在上层照常发生)。
			ClearState(tracking, combatId);
			creature.SetCurrentHpInternal(0);
			return NearDeathFeastRune.CreateDamageResult(creature, props, hpLoss, true, Math.Max(0, -deathLimit - newEffectiveHp));
		}

		bool dying = newEffectiveHp < 1;
		if (dying)
		{
			tracking.NearDeathFeastEnemyDebt[combatId] = Math.Max(0, -newEffectiveHp);
		}
		else
		{
			ClearState(tracking, combatId);
		}

		creature.SetCurrentHpInternal(dying ? 1 : newEffectiveHp);
		if (dying)
		{
			_ = SyncStrength(creature, modifier, combatId);
		}

		return NearDeathFeastRune.CreateDamageResult(creature, props, hpLoss, false, 0);
	}

	/// <summary>斩杀/处决类命令(CreatureCmd.Kill 系)无视濒死,直接判死。</summary>
	internal static void ForceDeathThresholdForKill(Creature creature)
	{
		if (TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId))
		{
			ClearState(modifier.CombatTracking, combatId);
			creature.SetCurrentHpInternal(0);
		}
	}

	/// <summary>CurrentHp 被外部设为负值时,转化为濒死债务(与玩家版 setter 拦截对应)。</summary>
	internal static void PreserveNegativeHpAsDyingState(Creature creature, int requestedHp)
	{
		if (!TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId))
		{
			creature.SetCurrentHpInternal(Math.Max(0, requestedHp));
			return;
		}

		HextechMayhemCombatTrackingState tracking = modifier.CombatTracking;
		int deathLimit = GetDeathNegativeHpLimit(creature, modifier);
		int debt = Math.Max(0, -requestedHp);
		if (debt >= deathLimit)
		{
			ClearState(tracking, combatId);
			creature.SetCurrentHpInternal(0);
			return;
		}

		tracking.NearDeathFeastEnemyDebt[combatId] = debt;
		creature.SetCurrentHpInternal(1);
		_ = SyncStrength(creature, modifier, combatId);
	}

	/// <summary>
	/// HP 被设回正值(转阶段满血、接续、复活、治疗直设)时清掉残留濒死状态,
	/// 避免新阶段仍被判为"濒死中"而禁疗禁格挡。
	/// </summary>
	internal static void ClearIfRecovered(Creature creature, int newHp)
	{
		if (newHp >= 1 && TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId))
		{
			ClearState(modifier.CombatTracking, combatId);
		}
	}

	internal static bool TryGetDisplayedHp(Creature creature, out int displayedHp)
	{
		displayedHp = 0;
		if (!TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId)
			|| !modifier.CombatTracking.NearDeathFeastEnemyDebt.TryGetValue(combatId, out int debt))
		{
			return false;
		}

		displayedHp = -debt;
		return true;
	}

	/// <summary>纯只读:供特效层轮询濒死强度(债务/死亡上限,0..1)。</summary>
	internal static bool TryGetFeastIntensity(Creature creature, out float intensity)
	{
		intensity = 0f;
		if (!TryGetContext(creature, out HextechMayhemModifier? modifier, out uint combatId)
			|| creature.CurrentHp < 1
			|| !modifier.CombatTracking.NearDeathFeastEnemyDebt.TryGetValue(combatId, out int debt))
		{
			return false;
		}

		int limit = GetDeathNegativeHpLimit(creature, modifier);
		intensity = limit > 0 ? Math.Clamp(debt / (float)limit, 0f, 1f) : 0f;
		return true;
	}

	internal static int GetDeathNegativeHpLimit(Creature creature, HextechMayhemModifier modifier)
	{
		int tier = Math.Clamp(modifier.GetMonsterHexStrengthTier(MonsterHexKind.NearDeathFeast), 1, 3);
		return Math.Max(1, (int)Math.Floor(creature.MaxHp * LimitPercentPerTier * tier));
	}

	private static bool TryGetContext(Creature creature, out HextechMayhemModifier modifier, out uint combatId)
	{
		modifier = null!;
		combatId = 0;
		if (creature.Side != CombatSide.Enemy
			|| creature.CombatId is not { } id
			|| creature.CombatState?.RunState is not { } runState)
		{
			return false;
		}

		HextechMayhemModifier? found = runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
		if (found == null || !found.HasActiveMonsterHex(MonsterHexKind.NearDeathFeast))
		{
			return false;
		}

		modifier = found;
		combatId = id;
		return true;
	}

	private static void ClearState(HextechMayhemCombatTrackingState tracking, uint combatId)
	{
		tracking.NearDeathFeastEnemyDebt.Remove(combatId);
		tracking.NearDeathFeastEnemyStrength.Remove(combatId);
	}

	/// <summary>把力量补到"每 1 负血 1 层"的目标值(只补差额、只增不减,与玩家版一致)。</summary>
	private static async Task SyncStrength(Creature creature, HextechMayhemModifier modifier, uint combatId)
	{
		SemaphoreSlim syncGate = StrengthSyncGates.GetValue(creature, static _ => new SemaphoreSlim(1, 1));
		await syncGate.WaitAsync();
		HextechMayhemCombatTrackingState? tracking = null;
		int previousGranted = 0;
		int reservedGranted = 0;
		bool hadPreviousEntry = false;
		try
		{
			tracking = modifier.CombatTracking;
			if (!tracking.NearDeathFeastEnemyDebt.TryGetValue(combatId, out int debt))
			{
				return;
			}

			hadPreviousEntry = tracking.NearDeathFeastEnemyStrength.TryGetValue(combatId, out previousGranted);
			int delta = debt - previousGranted;
			if (delta <= 0)
			{
				return;
			}

			reservedGranted = debt;
			tracking.NearDeathFeastEnemyStrength[combatId] = reservedGranted;
			await PowerCmd.Apply<StrengthPower>(creature, delta, creature, null);
		}
		catch (Exception ex)
		{
			if (tracking != null
				&& tracking.NearDeathFeastEnemyStrength.TryGetValue(combatId, out int currentGranted)
				&& currentGranted == reservedGranted)
			{
				if (hadPreviousEntry)
				{
					tracking.NearDeathFeastEnemyStrength[combatId] = previousGranted;
				}
				else
				{
					tracking.NearDeathFeastEnemyStrength.Remove(combatId);
				}
			}

			Log.Warn($"[{ModInfo.Id}][NearDeathFeast] Enemy strength sync failed: {ex.Message}");
		}
		finally
		{
			syncGate.Release();
		}
	}
}
