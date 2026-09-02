using MegaCrit.Sts2.Core.Models.Monsters;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechEncounterCompatibilityHooks
{
	private const string EntomancerCastSfx = "event:/sfx/enemy/enemy_attacks/entomancer/entomancer_cast";

	internal static bool ShouldRunOriginalEntomancerSpitMove(bool hasPersonalHive)
	{
#if STS2_107_1
		return hasPersonalHive;
#else
		return true;
#endif
	}

#if STS2_107_1
	internal static MethodInfo? TryResolveEntomancerSpitMove(Type entomancerType, bool warnIfMissing)
	{
		return TryGetMethod(
			entomancerType,
			"SpitMove",
			BindingFlags.Instance | BindingFlags.NonPublic,
			warnIfMissing,
			typeof(IReadOnlyList<Creature>));
	}

	private static async Task EntomancerSpitMoveWithoutPersonalHive(Entomancer entomancer)
	{
		SfxCmd.Play(EntomancerCastSfx);
		await CreatureCmd.TriggerAnim(entomancer.Creature, "Cast", 0.5f);
		await PowerCmd.Apply<StrengthPower>(entomancer.Creature, 2m, entomancer.Creature, null);
	}

	// 0.107.1 的昆虫法师在没有私人蜂巢时 SpitMove 会空引用;0.108 起原版已修复。
	[HarmonyPatch]
	[HextechPatch("compat.entomancer-spit", "昆虫法师遭遇战兼容", Optional = true)]
	private static class EntomancerSpitMovePatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => TryResolveEntomancerSpitMove(typeof(Entomancer), warnIfMissing: true) != null;

		[HarmonyTargetMethod]
		private static MethodBase TargetMethod() => TryResolveEntomancerSpitMove(typeof(Entomancer), warnIfMissing: false)!;

		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Entomancer __instance, ref Task __result)
		{
			if (ShouldRunOriginalEntomancerSpitMove(__instance.Creature.HasPower<PersonalHivePower>()))
			{
				return true;
			}

			__result = EntomancerSpitMoveWithoutPersonalHive(__instance);
			return false;
		}
	}
#endif
}
