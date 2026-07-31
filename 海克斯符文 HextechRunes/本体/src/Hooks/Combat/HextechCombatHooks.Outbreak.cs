namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static readonly HextechScopedDepthGuard OutbreakPowerPoisonResponseGuard = new();
	private static readonly HextechScopedDepthGuard SleightOfFleshPowerDebuffResponseGuard = new();
	private static readonly HextechScopedDepthGuard CompensationReplacementGuard = new();

	// 即死符文在血肉戏法/疫情响应链内不能同步 DoomKill(死亡处理与进行中的
	// power hook 链撞车会卡死游戏),先挂账,响应链退出后统一补杀。
	private static readonly List<Creature> PendingInstantDeathDoomKills = [];

	internal static bool IsResolvingOutbreakPowerPoisonResponse => OutbreakPowerPoisonResponseGuard.IsActive;
	internal static bool IsResolvingSleightOfFleshPowerDebuffResponse => SleightOfFleshPowerDebuffResponseGuard.IsActive;
	internal static bool IsApplyingCompensationReplacement => CompensationReplacementGuard.IsActive;

	internal static void QueueInstantDeathDoomKill(Creature creature)
	{
		if (!PendingInstantDeathDoomKills.Contains(creature))
		{
			PendingInstantDeathDoomKills.Add(creature);
		}
	}

	private static async Task FlushPendingInstantDeathDoomKillsIfSafe()
	{
		if (SleightOfFleshPowerDebuffResponseGuard.IsActive || OutbreakPowerPoisonResponseGuard.IsActive)
		{
			return;
		}

		while (PendingInstantDeathDoomKills.Count > 0)
		{
			Creature creature = PendingInstantDeathDoomKills[0];
			PendingInstantDeathDoomKills.RemoveAt(0);
			if (creature.IsAlive && creature.GetPowerAmount<DoomPower>() > creature.CurrentHp)
			{
				await DoomPower.DoomKill([creature]);
			}
		}
	}

#if STS2_110_OR_NEWER
	// 0.110.0 将疫情从持续监听中毒施加的 OutbreakPower 重做为技能牌:
	// 整个 OnPlay 内先施加中毒再主动触发。守卫覆盖这段完整响应链,维持即死与补偿的安全边界。
	private static void OutbreakOnPlayPrefix(out bool __state)
	{
		__state = true;
		OutbreakPowerPoisonResponseGuard.Enter();
	}

	private static void OutbreakOnPlayPostfix(bool __state, ref Task __result)
	{
		if (__state)
		{
			__result = OutbreakPowerPoisonResponseGuard.WrapEnteredTask(
				__result,
				FlushPendingInstantDeathDoomKillsIfSafe);
		}
	}
#else
	private static void OutbreakPowerAfterPowerAmountChangedPrefix(OutbreakPower __instance, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource, out bool __state)
	{
		__state = amount > 0m
			&& applier == __instance.Owner
			&& power is PoisonPower;
		if (__state)
		{
			OutbreakPowerPoisonResponseGuard.Enter();
		}
	}

	private static void OutbreakPowerAfterPowerAmountChangedPostfix(bool __state, ref Task __result)
	{
		if (__state)
		{
			__result = OutbreakPowerPoisonResponseGuard.WrapEnteredTask(
				__result,
				FlushPendingInstantDeathDoomKillsIfSafe);
		}
	}
#endif

	private static bool SleightOfFleshPowerAfterPowerAmountChangedPrefix(SleightOfFleshPower __instance, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource, ref Task __result, out bool __state)
	{
		__state = false;
		bool wouldRespond = IsSleightOfFleshPowerDebuffResponse(__instance, power, amount, applier);
		if (ShouldSuppressSleightOfFleshPowerDebuffResponse(wouldRespond))
		{
			__result = Task.CompletedTask;
			return false;
		}

		if (wouldRespond)
		{
			__state = true;
			SleightOfFleshPowerDebuffResponseGuard.Enter();
		}

		return true;
	}

	private static void SleightOfFleshPowerAfterPowerAmountChangedPostfix(bool __state, ref Task __result)
	{
		if (__state)
		{
			__result = SleightOfFleshPowerDebuffResponseGuard.WrapEnteredTask(
				__result,
				FlushPendingInstantDeathDoomKillsIfSafe);
		}
	}

	private static bool IsSleightOfFleshPowerDebuffResponse(SleightOfFleshPower instance, PowerModel power, decimal amount, Creature? applier)
	{
		return amount != 0m
			&& power.GetTypeForAmount(amount) == PowerType.Debuff
			&& power.Owner.IsEnemy
			&& applier == instance.Owner
			&& power is not ITemporaryPower;
	}

	internal static bool ShouldSuppressSleightOfFleshPowerDebuffResponse(bool wouldRespond)
	{
		return wouldRespond && IsApplyingCompensationReplacement;
	}

	internal static Task RunWithOutbreakPowerPoisonResponseGuard(Func<Task> action)
	{
		return OutbreakPowerPoisonResponseGuard.RunAsync(action);
	}

	internal static Task RunWithCompensationReplacementGuard(Func<Task> action)
	{
		return CompensationReplacementGuard.RunAsync(action);
	}

	internal static Task RunWithSleightOfFleshPowerDebuffResponseGuard(Func<Task> action)
	{
		return SleightOfFleshPowerDebuffResponseGuard.RunAsync(action);
	}
}
