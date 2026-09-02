namespace HextechRunes;

internal static partial class HextechCombatHooks
{

	[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal), typeof(decimal), typeof(ValueProp))]
	[HextechPatch("combat.piercing-thread.block", "穿刺之线")]
	private static class DamageBlockPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		private static void Prefix(Creature __instance, ref decimal amount, ValueProp props)
		{
			if (PiercingThreadRune.TryTakeBlockableDamage(
				CurrentActualDamageCommandId,
				__instance,
				amount,
				props,
				out decimal blockableDamage))
			{
				amount = blockableDamage;
			}
		}
	}
}
