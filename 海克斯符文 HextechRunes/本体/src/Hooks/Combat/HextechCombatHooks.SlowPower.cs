namespace HextechRunes;

internal static partial class HextechCombatHooks
{

	internal static bool TryResolveNeutralPowerType(PowerModel power, out PowerType powerType)
	{
		if (power is HextechPlayerSlowPower or HextechTemporarySlowPower)
		{
			// 原版会把 Counter+AllowNegative 的负层强制判为 Debuff；缓慢的正负层只表达受伤倍率方向。
			powerType = PowerType.None;
			return true;
		}

		powerType = default;
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.GetTypeForAmount), typeof(decimal))]
	[HextechPatch("combat.power-type-for-amount", "中性能力类型")]
	private static class PowerTypeForAmountPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(
			PowerModel __instance,
			ref PowerType __result)
		{
			if (!TryResolveNeutralPowerType(__instance, out PowerType resolvedType))
			{
				return true;
			}

			__result = resolvedType;
			return false;
		}
	}
}
