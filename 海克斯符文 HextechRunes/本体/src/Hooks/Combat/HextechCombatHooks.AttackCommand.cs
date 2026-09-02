namespace HextechRunes;

internal static partial class HextechCombatHooks
{

	internal static async Task<AttackCommand> EnsureAttackCommandExecuteResult(Task<AttackCommand>? task, AttackCommand command)
	{
		if (task == null)
		{
			return command;
		}

		return await task ?? command;
	}

	[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute), typeof(PlayerChoiceContext))]
	[HextechPatch("combat.attack-command.result", "攻击命令结果兜底")]
	private static class AttackCommandExecutePatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result)
		{
			__result = EnsureAttackCommandExecuteResult(__result, __instance);
		}
	}
}
