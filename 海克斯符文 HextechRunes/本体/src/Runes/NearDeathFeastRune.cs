using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

public sealed class NearDeathFeastRune : HextechRelicBase
{
	private const int DeathNegativeMaxHpDivisor = 2;
	private static readonly object MissingDamageResultMemberLogLock = new();
	private static readonly HashSet<string> LoggedMissingDamageResultMembers = [];
	private static readonly ConditionalWeakTable<NearDeathFeastRune, SemaphoreSlim> StrengthSyncGates = new();
	private bool _nearDeathActive;
	private int _nearDeathDebt;
	private int _nearDeathStrengthBonus;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedNearDeathActive
	{
		get => _nearDeathActive;
		set => _nearDeathActive = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedNearDeathDebt
	{
		get => _nearDeathDebt;
		set => _nearDeathDebt = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedNearDeathStrengthBonus
	{
		get => _nearDeathStrengthBonus;
		set => _nearDeathStrengthBonus = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DeathNegativeMaxHpPercent", 50m),
		new DynamicVar("StrengthPerNegativeHp", 1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<StrengthPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override bool ShowCounter => Owner != null && !IsCanonical;

	public override int DisplayAmount => Owner != null ? GetDeathNegativeHpLimit(Owner.Creature) : 0;

	internal static bool HasDyingState(Creature creature)
	{
		return creature.Player?.GetRelic<NearDeathFeastRune>() != null;
	}

	internal static bool IsDyingButAlive(Creature creature)
	{
		NearDeathFeastRune? rune = GetRune(creature);
		return rune != null
			&& rune._nearDeathActive
			&& creature.CurrentHp > 0
			&& rune._nearDeathDebt < GetDeathNegativeHpLimit(creature);
	}

	/// <summary>
	/// 纯只读:供特效层轮询「濒死狂宴」是否激活及其强度(负血债务 / 死亡上限,0..1)。
	/// 不修改任何状态,仅反映已同步的运行状态,各端轮询结果一致。
	/// </summary>
	internal static bool TryGetFeastIntensity(Creature creature, out float intensity)
	{
		intensity = 0f;
		NearDeathFeastRune? rune = GetRune(creature);
		if (rune == null || !rune._nearDeathActive || creature.CurrentHp < 1)
		{
			return false;
		}

		int limit = GetDeathNegativeHpLimit(creature);
		intensity = limit > 0 ? Math.Clamp(rune._nearDeathDebt / (float)limit, 0f, 1f) : 0f;
		return true;
	}

	internal static bool ShouldPreventSustain(Creature creature)
	{
		return IsDyingButAlive(creature);
	}

	internal static bool ShouldInterceptLoseHp(Creature creature, decimal amount)
	{
		NearDeathFeastRune? rune = GetRune(creature);
		if (rune == null || amount <= 0m)
		{
			return false;
		}

		int hpLoss = (int)Math.Min(amount, 999999999m);
		return rune._nearDeathActive || creature.CurrentHp - hpLoss < 1;
	}

	internal static DamageResult LoseHpAllowingDying(Creature creature, decimal amount, ValueProp props)
	{
		NearDeathFeastRune? rune = GetRune(creature);
		if (rune == null)
		{
			return CreateDamageResult(creature, props, 0, false, 0);
		}

		if (amount <= 0m)
		{
			return CreateDamageResult(creature, props, 0, false, 0);
		}

		int oldEffectiveHp = rune._nearDeathActive ? -rune._nearDeathDebt : creature.CurrentHp;
		int hpLoss = (int)Math.Min(amount, 999999999m);
		int newEffectiveHp = oldEffectiveHp - hpLoss;
		int deathLimit = GetDeathNegativeHpLimit(creature);
		bool killed = newEffectiveHp <= -deathLimit;

		if (killed)
		{
			rune._nearDeathActive = false;
			rune._nearDeathDebt = deathLimit;
			creature.SetCurrentHpInternal(0);
			return CreateDamageResult(creature, props, hpLoss, true, Math.Max(0, -deathLimit - newEffectiveHp));
		}

		rune._nearDeathActive = newEffectiveHp < 1;
		rune._nearDeathDebt = Math.Max(0, -newEffectiveHp);
		int safeHp = rune._nearDeathActive ? 1 : newEffectiveHp;
		bool hpChanged = creature.CurrentHp != safeHp;
		creature.SetCurrentHpInternal(safeHp);
		if (!hpChanged)
		{
			// 同步伤害 hook 不能等待异步力量命令；交给安全任务包装保留异常证据。
			_ = TaskHelper.RunSafely(rune.SyncNearDeathStrength());
		}

		return CreateDamageResult(creature, props, hpLoss, false, 0);
	}

	internal static void ForceDeathThresholdForKill(Creature creature)
	{
		NearDeathFeastRune? rune = GetRune(creature);
		if (rune != null)
		{
			rune._nearDeathActive = false;
			rune._nearDeathDebt = GetDeathNegativeHpLimit(creature);
			creature.SetCurrentHpInternal(0);
		}
	}

	internal static void PreserveNegativeHpAsDyingState(Creature creature, int requestedHp)
	{
		NearDeathFeastRune? rune = GetRune(creature);
		if (rune == null)
		{
			creature.SetCurrentHpInternal(Math.Max(0, requestedHp));
			return;
		}

		int deathLimit = GetDeathNegativeHpLimit(creature);
		int debt = Math.Max(0, -requestedHp);
		if (debt >= deathLimit)
		{
			rune._nearDeathActive = false;
			rune._nearDeathDebt = deathLimit;
			creature.SetCurrentHpInternal(0);
			return;
		}

		rune._nearDeathActive = true;
		rune._nearDeathDebt = debt;
		creature.SetCurrentHpInternal(1);
		_ = TaskHelper.RunSafely(rune.SyncNearDeathStrength());
	}

	internal static int GetDeathNegativeHpLimit(Creature creature)
	{
		return Math.Max(1, FloorToInt(creature.MaxHp / (decimal)DeathNegativeMaxHpDivisor));
	}

	internal static bool TryGetDisplayedHp(Creature creature, out int displayedHp)
	{
		NearDeathFeastRune? rune = GetRune(creature);
		if (rune != null && rune._nearDeathActive)
		{
			displayedHp = -rune._nearDeathDebt;
			return true;
		}

		displayedHp = 0;
		return false;
	}

	internal void RefreshDeathLimitDisplay()
	{
		InvokeDisplayAmountChanged();
	}

	public override Task AfterObtained()
	{
		RefreshDeathLimitDisplay();
		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		RefreshDeathLimitDisplay();
		return Task.CompletedTask;
	}

	public override Task BeforeCombatStart()
	{
		ResetNearDeathState();
		RefreshDeathLimitDisplay();
		return Task.CompletedTask;
	}

	public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
	{
		if (Owner == null || creature != Owner.Creature || !_nearDeathActive)
		{
			return;
		}

		await SyncNearDeathStrength();
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner != null && (_nearDeathActive || Owner.Creature.CurrentHp < 1))
		{
			Flash(Array.Empty<Creature>());
			Owner.Creature.SetCurrentHpInternal(1);
		}

		ResetNearDeathState();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetNearDeathState();
		return Task.CompletedTask;
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature && ShouldPreventSustain(target) ? 0m : 1m;
	}

	private async Task SyncNearDeathStrength()
	{
		SemaphoreSlim syncGate = StrengthSyncGates.GetValue(this, static _ => new SemaphoreSlim(1, 1));
		await syncGate.WaitAsync();
		int previousBonus = 0;
		int reservedBonus = 0;
		bool bonusReserved = false;
		try
		{
			if (Owner is not Player owner)
			{
				return;
			}

			int desiredBonus = _nearDeathActive
				? _nearDeathDebt * (int)DynamicVars["StrengthPerNegativeHp"].BaseValue
				: 0;
			previousBonus = _nearDeathStrengthBonus;
			int delta = desiredBonus - previousBonus;
			if (delta <= 0)
			{
				_nearDeathStrengthBonus = desiredBonus;
				return;
			}

			reservedBonus = desiredBonus;
			_nearDeathStrengthBonus = reservedBonus;
			bonusReserved = true;
			Flash();
			await PowerCmd.Apply<StrengthPower>(owner.Creature, delta, owner.Creature, null);
		}
		catch
		{
			if (bonusReserved && _nearDeathStrengthBonus == reservedBonus)
			{
				_nearDeathStrengthBonus = previousBonus;
			}

			throw;
		}
		finally
		{
			syncGate.Release();
		}
	}

	private void ResetNearDeathState()
	{
		_nearDeathActive = false;
		_nearDeathDebt = 0;
		_nearDeathStrengthBonus = 0;
	}

	private static NearDeathFeastRune? GetRune(Creature creature)
	{
		return creature.Player?.GetRelic<NearDeathFeastRune>();
	}

	// 供敌方濒死狂宴(HextechEnemyNearDeath)复用同一套反射构造。
	internal static DamageResult CreateDamageResult(Creature creature, ValueProp props, int unblockedDamage, bool wasTargetKilled, int overkillDamage)
	{
		DamageResult result = new(creature, props);
		object boxed = result;
		SetDamageResultValue(boxed, nameof(DamageResult.UnblockedDamage), unblockedDamage);
		SetDamageResultValue(boxed, nameof(DamageResult.WasTargetKilled), wasTargetKilled);
		SetDamageResultValue(boxed, nameof(DamageResult.OverkillDamage), overkillDamage);
		return (DamageResult)boxed;
	}

	private static void SetDamageResultValue(object result, string memberName, object value)
	{
		Type type = result.GetType();
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		PropertyInfo? property = type.GetProperty(memberName, flags);
		if (property?.SetMethod != null)
		{
			property.SetValue(result, ConvertDamageResultValue(value, property.PropertyType));
			return;
		}

		FieldInfo? field = type.GetField($"<{memberName}>k__BackingField", flags)
			?? type.GetField(memberName, flags)
			?? type.GetField($"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}", flags);
		if (field != null)
		{
			field.SetValue(result, ConvertDamageResultValue(value, field.FieldType));
			return;
		}

		lock (MissingDamageResultMemberLogLock)
		{
			if (!LoggedMissingDamageResultMembers.Add($"{type.AssemblyQualifiedName}:{memberName}"))
			{
				return;
			}
		}

		Log.Warn($"[{ModInfo.Id}][Reflection] Missing writable DamageResult member {type.FullName}.{memberName}; result field left at its default.");
	}

	private static object ConvertDamageResultValue(object value, Type targetType)
	{
		Type actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
		return Convert.ChangeType(value, actualType);
	}
}
