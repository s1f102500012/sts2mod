using System.Diagnostics;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static readonly AsyncLocal<long[]?> ActualDamageCommandIds = new();
	private static readonly HashSet<string> LoggedPreCombatDamageSuppressions = [];
	private static readonly object PreCombatDamageSuppressionLogLock = new();
	private static long _nextActualDamageCommandId;

	internal static long CurrentActualDamageCommandId
	{
		get
		{
			long[]? ids = ActualDamageCommandIds.Value;
			return ids is { Length: > 0 } ? ids[^1] : 0L;
		}
	}

	private static bool ActualDamageCommandPrefix(IEnumerable<Creature> targets, out long __state, ref Task<IEnumerable<DamageResult>> __result)
	{
		if (ShouldSuppressPreCombatEnemyDamage(targets))
		{
			__state = 0L;
			__result = Task.FromResult(Enumerable.Empty<DamageResult>());
			return false;
		}

		__state = Interlocked.Increment(ref _nextActualDamageCommandId);
		long[] current = ActualDamageCommandIds.Value ?? [];
		long[] next = new long[current.Length + 1];
		Array.Copy(current, next, current.Length);
		next[^1] = __state;
		ActualDamageCommandIds.Value = next;
		return true;
	}

	private static bool ShouldSuppressPreCombatEnemyDamage(IEnumerable<Creature> targets)
	{
		if (CombatManager.Instance?.IsInProgress == true)
		{
			return false;
		}

		foreach (Creature target in targets)
		{
			if (target is not { Side: CombatSide.Enemy, CombatState.RunState: RunState runState }
				|| !HasActiveHextechCombatContent(runState))
			{
				continue;
			}

			WarnPreCombatDamageSuppressed(target);
			return true;
		}

		return false;
	}

	private static bool HasActiveHextechCombatContent(RunState runState)
	{
		return runState.Players.Any(static player => player.Relics.Any(HextechCatalog.IsHextechRelic))
			|| HextechMayhemModifier.FindIn(runState)?.GetActiveMonsterHexes().Count > 0;
	}

	private static void WarnPreCombatDamageSuppressed(Creature target)
	{
		MethodBase? caller = new StackTrace(skipFrames: 1, fNeedFileInfo: false)
			.GetFrames()
			.Select(static frame => frame.GetMethod())
			.FirstOrDefault(static method => method?.DeclaringType != typeof(HextechCombatHooks));
		string callerName = caller == null
			? "<unknown>"
			: $"{caller.DeclaringType?.FullName ?? "<unknown>"}.{caller.Name}";
		string targetId = target.ModelId.Entry;
		string key = $"{targetId}:{callerName}";

		lock (PreCombatDamageSuppressionLogLock)
		{
			if (!LoggedPreCombatDamageSuppressions.Add(key))
			{
				return;
			}
		}

		Log.Warn($"[{ModInfo.Id}][Mayhem] Suppressed pre-combat enemy damage: target={targetId} combatId={target.CombatId?.ToString() ?? "none"} caller={callerName}");
	}

	private static void ActualDamageCommandPostfix(long __state, ref Task<IEnumerable<DamageResult>> __result)
	{
		if (__state != 0L)
		{
			__result = CompleteWithActualDamageCommandReset(__result, __state);
		}
	}

	private static async Task<T> CompleteWithActualDamageCommandReset<T>(Task<T> task, long commandId)
	{
		try
		{
			return await task;
		}
		finally
		{
			PopActualDamageCommand(commandId);
			PacifistRune.ClearPendingDoomApplications(commandId);
			CompensationRune.ClearPendingCompensations(commandId);
			CompensationEnemyHex.ClearPendingCompensations(commandId);
			await ConsumeOstyRedirectedSlippery(commandId);
			ClearSlipperyReductions(commandId);
		}
	}

	private static void PopActualDamageCommand(long commandId)
	{
		long[]? current = ActualDamageCommandIds.Value;
		if (current is not { Length: > 0 })
		{
			return;
		}

		long[] next;
		if (current[^1] == commandId)
		{
			next = current[..^1];
		}
		else
		{
			next = current.Where(id => id != commandId).ToArray();
		}

		ActualDamageCommandIds.Value = next.Length == 0 ? null : next;
	}
}
