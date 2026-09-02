using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

// PersonalHivePower 原版只为敌方养蜂人设计:受击时会把晕眩加入攻击者玩家的牌堆。
// 薄暮法衣旧版本可能已把它写进进行中的玩家战斗状态;这里不在 Hook 枚举期间移除能力,
// 只让非法的非敌方实例安全完成回调,以便原版伤害响应链正常 PopModel/收尾。
internal static class HextechPersonalHiveSafetyHooks
{

	internal static MethodInfo ResolveDamageResponseTarget()
	{
		return RequireMethod(
			typeof(PersonalHivePower),
			nameof(PersonalHivePower.AfterDamageReceived),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(PlayerChoiceContext),
			typeof(Creature),
			typeof(DamageResult),
			typeof(ValueProp),
			typeof(Creature),
			typeof(CardModel));
	}

	internal static bool ShouldRunOriginal(CombatSide? ownerSide)
	{
		return ownerSide == CombatSide.Enemy;
	}


	[HarmonyPatch]
	[HextechPatch("compat.personal-hive", "私人蜂巢伤害响应安全")]
	private static class DamageResponsePatch
	{
		[HarmonyTargetMethod]
		private static MethodBase TargetMethod() => ResolveDamageResponseTarget();

		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(PersonalHivePower __instance, ref Task __result)
		{
			if (ShouldRunOriginal(__instance.Owner?.Side))
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}
}
