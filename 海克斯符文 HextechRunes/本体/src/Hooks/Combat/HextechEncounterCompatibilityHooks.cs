using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Monsters;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechEncounterCompatibilityHooks
{
	private const string EntomancerCastSfx = "event:/sfx/enemy/enemy_attacks/entomancer/entomancer_cast";

	public static void Install(Harmony harmony)
	{
	#if STS2_107_1 && !STS2_108_OR_NEWER
		MethodInfo? spitMove = TryResolveEntomancerSpitMove(typeof(Entomancer), warnIfMissing: true);
		if (spitMove == null)
		{
			return;
		}

		harmony.Patch(
			spitMove,
			prefix: new HarmonyMethod(typeof(HextechEncounterCompatibilityHooks), nameof(EntomancerSpitMovePrefix)));
	#endif
	}

	internal static bool ShouldRunOriginalEntomancerSpitMove(bool hasPersonalHive)
	{
#if STS2_107_1 && !STS2_108_OR_NEWER
		return hasPersonalHive;
#else
		return true;
#endif
	}

	#if STS2_107_1 && !STS2_108_OR_NEWER
	internal static MethodInfo? TryResolveEntomancerSpitMove(Type entomancerType, bool warnIfMissing)
	{
		return TryGetMethod(
			entomancerType,
			"SpitMove",
			BindingFlags.Instance | BindingFlags.NonPublic,
			warnIfMissing,
			typeof(IReadOnlyList<Creature>));
	}

	private static bool EntomancerSpitMovePrefix(Entomancer __instance, ref Task __result)
	{
		if (ShouldRunOriginalEntomancerSpitMove(__instance.Creature.HasPower<PersonalHivePower>()))
		{
			return true;
		}

		__result = EntomancerSpitMoveWithoutPersonalHive(__instance);
		return false;
	}

	private static async Task EntomancerSpitMoveWithoutPersonalHive(Entomancer entomancer)
	{
		SfxCmd.Play(EntomancerCastSfx);
		await CreatureCmd.TriggerAnim(entomancer.Creature, "Cast", 0.5f);
		await PowerCmd.Apply<StrengthPower>(entomancer.Creature, 2m, entomancer.Creature, null);
	}
#endif
}
