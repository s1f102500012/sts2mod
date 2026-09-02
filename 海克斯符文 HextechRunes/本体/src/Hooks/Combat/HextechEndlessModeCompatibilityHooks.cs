using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Monsters;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechEndlessModeCompatibilityHooks
{
	private const string EndlessModeAssemblyName = "EndlessMode";
	private const string EndlessModeEntryTypeName = "EndlessMode.ModEntry";
	private const string EndlessMultiplierMethodName = "GetEnemyEndlessScalingMultiplier";
	private const string TemporarySyncFixHarmonyId = "sts2.lblc.endless-hextech-sync-fix";

	private sealed class CapturedPowerAmounts
	{
		internal decimal? HardToKill;
		internal decimal? HardenedShell;
	}

	private static readonly ConditionalWeakTable<Creature, CapturedPowerAmounts> CapturedAmounts = new();
	private static readonly object EndlessMethodLock = new();
	private static MethodInfo? _endlessMultiplierMethod;
	private static bool _loggedMissingEndlessApi;
	private static bool _loggedNormalizationFailure;


	private static async Task NormalizeExoskeletonAfterOriginal(Task original, Exoskeleton monster)
	{
		await original;
		try
		{
			Creature creature = monster.Creature;
			if (!TryTakeCapturedAmount(creature, hardToKill: true, out decimal rawAmount)
				|| creature.GetPower<HardToKillPower>() is not HardToKillPower power
				|| !TryGetEndlessMultiplier(creature, out decimal multiplier))
			{
				return;
			}

			int expected = CalculateEndlessScaledAmount(rawAmount, multiplier);
			SetNormalizedAmount(creature, power, rawAmount, multiplier, expected, "HardToKill");
		}
		catch (Exception ex)
		{
			LogNormalizationFailureOnce(ex);
		}
	}

	private static async Task NormalizeSkulkingColonyAfterOriginal(Task original, SkulkingColony monster)
	{
		await original;
		try
		{
			Creature creature = monster.Creature;
			if (!TryTakeCapturedAmount(creature, hardToKill: false, out decimal rawAmount)
				|| creature.GetPower<HardenedShellPower>() is not HardenedShellPower power
				|| creature.CombatState == null
				|| !TryGetEndlessMultiplier(creature, out decimal multiplier))
			{
				return;
			}

			int endlessAmount = CalculateEndlessScaledAmount(rawAmount, multiplier);
			decimal multiplayerAmount = power.GetScaledAmountForMultiplayer(
				creature.CombatState,
				creature,
				endlessAmount,
				creature,
				null);
			int expected = ClampPowerAmountToInt(multiplayerAmount);
			SetNormalizedAmount(creature, power, rawAmount, multiplier, expected, "HardenedShell");
		}
		catch (Exception ex)
		{
			LogNormalizationFailureOnce(ex);
		}
	}

	private static bool TryTakeCapturedAmount(Creature creature, bool hardToKill, out decimal amount)
	{
		amount = 0m;
		if (!CapturedAmounts.TryGetValue(creature, out CapturedPowerAmounts? captured))
		{
			return false;
		}

		lock (captured)
		{
			decimal? value = hardToKill ? captured.HardToKill : captured.HardenedShell;
			if (hardToKill)
			{
				captured.HardToKill = null;
			}
			else
			{
				captured.HardenedShell = null;
			}

			if (captured.HardToKill == null && captured.HardenedShell == null)
			{
				CapturedAmounts.Remove(creature);
			}

			amount = value.GetValueOrDefault();
			return amount > 0m;
		}
	}

	internal static int CalculateEndlessScaledAmount(decimal rawAmount, decimal multiplier)
	{
		if (rawAmount <= 0m || multiplier <= 0m)
		{
			return 0;
		}

		try
		{
			return ClampPowerAmountToInt(Math.Ceiling(rawAmount * multiplier));
		}
		catch (OverflowException)
		{
			return int.MaxValue;
		}
	}

	private static int ClampPowerAmountToInt(decimal amount)
	{
		return amount >= int.MaxValue
			? int.MaxValue
			: amount <= int.MinValue
				? int.MinValue
				: (int)amount;
	}

	private static void SetNormalizedAmount(
		Creature creature,
		PowerModel power,
		decimal rawAmount,
		decimal multiplier,
		int expected,
		string label)
	{
		if (expected <= 0 || power.Amount == expected)
		{
			return;
		}

		int previous = power.Amount;
		power.SetAmount(expected, true);
		HextechLog.Info(
			$"[{ModInfo.Id}][EndlessCompat] Normalized {label}: combatId={creature.CombatId?.ToString() ?? "?"} " +
			$"raw={rawAmount} endlessMultiplier={multiplier} amount={previous}->{power.Amount}");
	}

	private static bool TryGetEndlessMultiplier(Creature creature, out decimal multiplier)
	{
		multiplier = 0m;
		try
		{
			MethodInfo? method = ResolveEndlessMultiplierMethod();
			if (method == null)
			{
				return false;
			}

			object? value = method.Invoke(null, [creature.CombatState?.RunState]);
			if (value is decimal resolved && resolved > 0m)
			{
				multiplier = resolved;
				return true;
			}
		}
		catch (Exception ex)
		{
			LogNormalizationFailureOnce(ex);
		}

		return false;
	}

	private static MethodInfo? ResolveEndlessMultiplierMethod()
	{
		if (_endlessMultiplierMethod != null)
		{
			return _endlessMultiplierMethod;
		}

		lock (EndlessMethodLock)
		{
			if (_endlessMultiplierMethod != null)
			{
				return _endlessMultiplierMethod;
			}

			Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
				static candidate => string.Equals(
					candidate.GetName().Name,
					EndlessModeAssemblyName,
					StringComparison.Ordinal));
			Type? entryType = assembly?.GetType(EndlessModeEntryTypeName, throwOnError: false);
			_endlessMultiplierMethod = entryType?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				.SingleOrDefault(static method =>
					method.Name == EndlessMultiplierMethodName
					&& method.ReturnType == typeof(decimal)
					&& method.GetParameters().Length == 1);

			if (assembly != null && _endlessMultiplierMethod == null && !_loggedMissingEndlessApi)
			{
				_loggedMissingEndlessApi = true;
				Log.Warn($"[{ModInfo.Id}][EndlessCompat] Endless enemy multiplier API not found; monster power normalization skipped.");
			}

			return _endlessMultiplierMethod;
		}
	}

	private static void LogNormalizationFailureOnce(Exception ex)
	{
		if (_loggedNormalizationFailure)
		{
			return;
		}

		_loggedNormalizationFailure = true;
		Exception cause = ex is TargetInvocationException invocation && invocation.InnerException is Exception inner
			? inner
			: ex;
		Log.Warn($"[{ModInfo.Id}][EndlessCompat] Monster power normalization failed: {cause.GetType().Name}: {cause.Message}");
	}

	[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd), nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.Apply), typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool))]
	[HextechPatch("compat.endless.apply-power", "无尽模式兼容")]
	private static class ApplyPowerCapturePatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		[HarmonyBefore(HextechCombatHooks.EndlessModeHarmonyId)]
		private static void Prefix(PowerModel power, Creature target, decimal amount)
		{
			if (amount <= 0m || target.Side != CombatSide.Enemy)
			{
				return;
			}

			CapturedPowerAmounts captured;
			if (target.Monster is Exoskeleton && power is HardToKillPower)
			{
				captured = CapturedAmounts.GetOrCreateValue(target);
				lock (captured)
				{
					captured.HardToKill = amount;
				}
			}
			else if (target.Monster is SkulkingColony && power is HardenedShellPower)
			{
				captured = CapturedAmounts.GetOrCreateValue(target);
				lock (captured)
				{
					captured.HardenedShell = amount;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Exoskeleton), nameof(Exoskeleton.AfterAddedToRoom), new Type[0])]
	[HextechPatch("compat.endless.exoskeleton", "无尽模式兼容")]
	private static class ExoskeletonPatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		[HarmonyAfter(HextechCombatHooks.EndlessModeHarmonyId, TemporarySyncFixHarmonyId)]
		private static void Postfix(Exoskeleton __instance, ref Task __result)
		{
			__result = NormalizeExoskeletonAfterOriginal(__result, __instance);
		}
	}

	[HarmonyPatch(typeof(SkulkingColony), nameof(SkulkingColony.AfterAddedToRoom), new Type[0])]
	[HextechPatch("compat.endless.skulking-colony", "无尽模式兼容")]
	private static class SkulkingColonyPatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		[HarmonyAfter(HextechCombatHooks.EndlessModeHarmonyId, TemporarySyncFixHarmonyId)]
		private static void Postfix(SkulkingColony __instance, ref Task __result)
		{
			__result = NormalizeSkulkingColonyAfterOriginal(__result, __instance);
		}
	}
}
