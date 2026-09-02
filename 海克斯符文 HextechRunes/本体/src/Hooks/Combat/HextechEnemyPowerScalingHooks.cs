using HarmonyLib;

namespace HextechRunes;

internal static partial class HextechEnemyPowerScalingHooks
{
	private enum ScalingOverride
	{
		Unscaled,
		PlayerCount,
		FinalAmount
	}

	private static readonly AsyncLocal<ScalingOverride?> CurrentOverride = new();


	public static async Task<T?> Apply<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
		where T : PowerModel
	{
		ScalingOverride? scalingOverride = GetScalingOverride(typeof(T));
		if (scalingOverride == null)
		{
			return await PowerCmd.Apply<T>(target, amount, applier, cardSource, silent);
		}

		decimal finalAmount = CalculateFinalAmount(target, amount, applier, scalingOverride.Value);
		finalAmount = ClampPowerOffsetForApply<T>(target, finalAmount);
		if (finalAmount == 0m)
		{
			return target.GetPower<T>();
		}

		Creature? effectiveApplier = ShouldClearSelfApplier(target, applier) ? null : applier;
		using (BeginOverride(ScalingOverride.FinalAmount))
		{
			return await PowerCmd.Apply<T>(target, finalAmount, effectiveApplier, cardSource, silent);
		}
	}

	/// <summary>
	/// 按原值应用,绕过原版联机缩放。原版 PowerCmd.Apply 对敌方目标且 ShouldScaleInMultiplayer
	/// 的 power(Slippery/Artifact 等)会自动 ×玩家数;层数已按最终口径算好的调用方(墨影幻灵)走这里。
	/// </summary>
	public static async Task<T?> ApplyExact<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
		where T : PowerModel
	{
		decimal finalAmount = ClampPowerOffsetForApply<T>(target, amount);
		if (finalAmount == 0m)
		{
			return target.GetPower<T>();
		}

		Creature? effectiveApplier = ShouldClearSelfApplier(target, applier) ? null : applier;
		using (BeginOverride(ScalingOverride.FinalAmount))
		{
			return await PowerCmd.Apply<T>(target, finalAmount, effectiveApplier, cardSource, silent);
		}
	}

	private static bool GetScaledAmountForMultiplayerPrefix(
		PowerModel __instance,
		decimal amount,
		Creature target,
		ref decimal __result)
	{
		if (CurrentOverride.Value != ScalingOverride.FinalAmount
			|| target == null
			|| (!target.IsPrimaryEnemy && !target.IsSecondaryEnemy)
			|| GetScalingOverride(__instance.GetType()) == null)
		{
			return true;
		}

		__result = ClampPowerOffsetForApply(__instance, target, amount);
		return false;
	}

	private static decimal CalculateFinalAmount(Creature target, decimal amount, Creature? applier, ScalingOverride scalingOverride)
	{
		if (!target.IsPrimaryEnemy && !target.IsSecondaryEnemy)
		{
			return amount;
		}

		return scalingOverride switch
		{
			ScalingOverride.PlayerCount => MultiplyByPlayerCount(amount, GetPlayerCount(applier, target)),
			ScalingOverride.Unscaled => ClampPowerAmount(amount),
			ScalingOverride.FinalAmount => ClampPowerAmount(amount),
			_ => ClampPowerAmount(amount)
		};
	}

	private static decimal ClampPowerOffsetForApply<T>(Creature target, decimal amount)
		where T : PowerModel
	{
		return ClampPowerOffsetForApply(ModelDb.Power<T>(), target, amount);
	}

	private static decimal ClampPowerOffsetForApply(PowerModel power, Creature target, decimal amount)
	{
		decimal clamped = ClampPowerAmount(amount);
		if (IsInstancedPower(power))
		{
			return clamped;
		}

		int currentAmount = target.GetPower(power.Id)?.Amount ?? 0;
		if (clamped > 0m)
		{
			decimal maxOffset = int.MaxValue - (decimal)currentAmount;
			return Math.Min(clamped, Math.Max(0m, maxOffset));
		}

		if (clamped < 0m)
		{
			decimal minOffset = int.MinValue - (decimal)currentAmount;
			return Math.Max(clamped, Math.Min(0m, minOffset));
		}

		return clamped;
	}

	private static bool IsInstancedPower(PowerModel power)
	{
		return power.InstanceType != PowerInstanceType.None;
	}

	private static bool ShouldClearSelfApplier(Creature target, Creature? applier)
	{
		return applier != null
			&& ReferenceEquals(target, applier)
			&& (target.IsPrimaryEnemy || target.IsSecondaryEnemy);
	}


	[HextechPatch("combat.enemy-power-scaling", "敌方能力联机缩放")]
	private static class ScaledAmountPatch
	{
		public static void Apply(Harmony harmony)
		{
			HarmonyMethod scaledPrefix = new(typeof(HextechEnemyPowerScalingHooks), nameof(GetScaledAmountForMultiplayerPrefix))
			{
				priority = Priority.First
			};

			foreach (MethodInfo scaledTarget in ResolveGetScaledAmountForMultiplayerTargets())
			{
				harmony.Patch(scaledTarget, prefix: scaledPrefix);
			}
		}
	}
}
