using MegaCrit.Sts2.Core.Logging;

namespace CustomDifficulty;

internal enum CustomDifficultyMode
{
	Fixed = 0,
	Progressive = 1
}

internal static class CustomDifficultySettings
{
	public const int MinTicks = 1;
	public const int MaxTicks = 50;
	public const int DefaultTicks = 10;

	// 递进模式：每前进一个房间，怪物血量/攻击的倍率增减的百分点。
	public const int MinDeltaPercent = -20;
	public const int MaxDeltaPercent = 20;
	public const int DefaultDeltaPercent = 2;

	// 递进倍率的安全边界：下限防止怪物变成空气，上限防止 int 溢出/数值爆炸。
	private const decimal MinProgressiveMultiplier = 0.1m;
	private const decimal MaxProgressiveMultiplier = 99m;

	private static int _monsterHpTicks = DefaultTicks;
	private static int _monsterAttackTicks = DefaultTicks;
	private static CustomDifficultyMode _mode = CustomDifficultyMode.Fixed;
	private static int _hpDeltaPercentPerRoom = DefaultDeltaPercent;
	private static int _attackDeltaPercentPerRoom = DefaultDeltaPercent;

	public static event Action? Changed;

	public static int MonsterHpTicks => _monsterHpTicks;

	public static int MonsterAttackTicks => _monsterAttackTicks;

	public static CustomDifficultyMode Mode => _mode;

	public static int HpDeltaPercentPerRoom => _hpDeltaPercentPerRoom;

	public static int AttackDeltaPercentPerRoom => _attackDeltaPercentPerRoom;

	public static decimal MonsterHpMultiplier => TicksToMultiplier(_monsterHpTicks);

	public static decimal MonsterAttackMultiplier => TicksToMultiplier(_monsterAttackTicks);

	public static double MonsterHpSliderValue => TicksToSliderValue(_monsterHpTicks);

	public static double MonsterAttackSliderValue => TicksToSliderValue(_monsterAttackTicks);

	// 递进模式的倍率：1 + 每房间增量 × 已走过的房间数，线性增长并 clamp 到安全区间。
	public static decimal GetHpMultiplierForFloor(int floorIndex)
	{
		return _mode == CustomDifficultyMode.Progressive
			? ProgressiveMultiplier(_hpDeltaPercentPerRoom, floorIndex)
			: MonsterHpMultiplier;
	}

	public static decimal GetAttackMultiplierForFloor(int floorIndex)
	{
		return _mode == CustomDifficultyMode.Progressive
			? ProgressiveMultiplier(_attackDeltaPercentPerRoom, floorIndex)
			: MonsterAttackMultiplier;
	}

	private static decimal ProgressiveMultiplier(int deltaPercent, int floorIndex)
	{
		decimal multiplier = 1m + deltaPercent / 100m * Math.Max(0, floorIndex);
		return Math.Clamp(multiplier, MinProgressiveMultiplier, MaxProgressiveMultiplier);
	}

	public static void SetLocal(int hpTicks, int attackTicks, CustomDifficultyMode mode, int hpDelta, int attackDelta, bool broadcast)
	{
		SetInternal(hpTicks, attackTicks, mode, hpDelta, attackDelta, broadcast, persist: true, "local");
	}

	public static void SetRemote(int hpTicks, int attackTicks, CustomDifficultyMode mode, int hpDelta, int attackDelta)
	{
		SetInternal(hpTicks, attackTicks, mode, hpDelta, attackDelta, broadcast: false, persist: false, "remote");
	}

	public static void SetPersisted(int hpTicks, int attackTicks, CustomDifficultyMode mode, int hpDelta, int attackDelta)
	{
		SetInternal(NormalizeTicks(hpTicks), NormalizeTicks(attackTicks), mode, hpDelta, attackDelta, broadcast: false, persist: false, "persisted");
	}

	public static int SliderValueToTicks(double value)
	{
		return ClampTicks((int)Math.Round(value * 10.0, MidpointRounding.AwayFromZero));
	}

	public static double TicksToSliderValue(int ticks)
	{
		return ClampTicks(ticks) / 10.0;
	}

	public static string FormatMultiplier(int ticks)
	{
		return $"x{TicksToSliderValue(ticks):0.0}";
	}

	public static string FormatDeltaPercent(int deltaPercent)
	{
		int clamped = ClampDeltaPercent(deltaPercent);
		return clamped >= 0 ? $"+{clamped}%" : $"{clamped}%";
	}

	public static decimal TicksToMultiplier(int ticks)
	{
		return ClampTicks(ticks) / 10m;
	}

	public static int NormalizeTicks(int ticks)
	{
		return ticks == 0 ? DefaultTicks : ClampTicks(ticks);
	}

	public static int ClampDeltaPercent(int deltaPercent)
	{
		return Math.Clamp(deltaPercent, MinDeltaPercent, MaxDeltaPercent);
	}

	public static CustomDifficultyMode NormalizeMode(int mode)
	{
		return mode == (int)CustomDifficultyMode.Progressive
			? CustomDifficultyMode.Progressive
			: CustomDifficultyMode.Fixed;
	}

	private static void SetInternal(int hpTicks, int attackTicks, CustomDifficultyMode mode, int hpDelta, int attackDelta, bool broadcast, bool persist, string source)
	{
		int clampedHpTicks = ClampTicks(hpTicks);
		int clampedAttackTicks = ClampTicks(attackTicks);
		int clampedHpDelta = ClampDeltaPercent(hpDelta);
		int clampedAttackDelta = ClampDeltaPercent(attackDelta);
		if (_monsterHpTicks == clampedHpTicks
			&& _monsterAttackTicks == clampedAttackTicks
			&& _mode == mode
			&& _hpDeltaPercentPerRoom == clampedHpDelta
			&& _attackDeltaPercentPerRoom == clampedAttackDelta)
		{
			return;
		}

		_monsterHpTicks = clampedHpTicks;
		_monsterAttackTicks = clampedAttackTicks;
		_mode = mode;
		_hpDeltaPercentPerRoom = clampedHpDelta;
		_attackDeltaPercentPerRoom = clampedAttackDelta;
		Log.Info($"[{ModInfo.Id}] Difficulty changed by {source}: mode={_mode} hp={FormatMultiplier(_monsterHpTicks)} attack={FormatMultiplier(_monsterAttackTicks)} hpDelta={FormatDeltaPercent(_hpDeltaPercentPerRoom)}/room attackDelta={FormatDeltaPercent(_attackDeltaPercentPerRoom)}/room.");
		Changed?.Invoke();

		if (persist)
		{
			CustomDifficultyStorage.SaveCurrentProfile();
		}

		if (broadcast)
		{
			CustomDifficultySync.BroadcastSettings();
		}
	}

	private static int ClampTicks(int ticks)
	{
		return Math.Clamp(ticks, MinTicks, MaxTicks);
	}
}
