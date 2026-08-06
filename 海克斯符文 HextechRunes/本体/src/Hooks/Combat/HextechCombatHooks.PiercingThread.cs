namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static void PiercingThreadDamageBlockPrefix(Creature __instance, ref decimal amount, ValueProp props)
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
