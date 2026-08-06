namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static readonly AsyncLocal<long[]?> ActualDamageCommandIds = new();
	private static long _nextActualDamageCommandId;

	internal static long CurrentActualDamageCommandId
	{
		get
		{
			long[]? ids = ActualDamageCommandIds.Value;
			return ids is { Length: > 0 } ? ids[^1] : 0L;
		}
	}

	private static void ActualDamageCommandPrefix(out long __state)
	{
		__state = Interlocked.Increment(ref _nextActualDamageCommandId);
		long[] current = ActualDamageCommandIds.Value ?? [];
		long[] next = new long[current.Length + 1];
		Array.Copy(current, next, current.Length);
		next[^1] = __state;
		ActualDamageCommandIds.Value = next;
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
			PiercingThreadRune.ClearPendingDamage(commandId);
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
