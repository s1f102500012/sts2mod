using System.Reflection;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace UniversalDominionSword;

internal static class ErasureTargeting
{
	private static readonly MethodInfo? CardGetter =
		AccessTools.PropertyGetter(typeof(NCardPlay), "Card");

	private static int _activeTargetingScopes;

	public static void Install(Harmony harmony)
	{
		PatchTargetingMethod(
			harmony,
			AccessTools.Method(
				typeof(NMouseCardPlay),
				"SingleCreatureTargeting",
				[typeof(TargetMode), typeof(TargetType)]));

		PatchTargetingMethod(
			harmony,
			AccessTools.Method(
				typeof(NControllerCardPlay),
				"SingleCreatureTargeting",
				[typeof(TargetType)]));

		MethodInfo? isHittableGetter =
			AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.IsHittable));
		if (isHittableGetter == null)
		{
			throw new MissingMethodException(
				$"Could not find the STS2 method required by {nameof(ErasureTargeting)}.");
		}

		harmony.Patch(
			isHittableGetter,
			postfix: new HarmonyMethod(
				typeof(ErasureTargeting),
				nameof(IsHittablePostfix)));
	}

	private static void TargetingPrefix(object __instance, out bool __state)
	{
		CardModel? card = CardGetter?.Invoke(__instance, null) as CardModel;
		__state = card is UniversalDominionSwordCard;
		if (__state)
		{
			Interlocked.Increment(ref _activeTargetingScopes);
		}
	}

	private static void TargetingPostfix(bool __state, ref Task __result)
	{
		if (__state)
		{
			__result = EndScopeWhenFinished(__result);
		}
	}

	private static async Task EndScopeWhenFinished(Task targetingTask)
	{
		try
		{
			await targetingTask;
		}
		finally
		{
			Interlocked.Decrement(ref _activeTargetingScopes);
		}
	}

	private static void IsHittablePostfix(Creature __instance, ref bool __result)
	{
		if (__result || Volatile.Read(ref _activeTargetingScopes) <= 0)
		{
			return;
		}

		if (__instance.Side == CombatSide.Enemy
			&& __instance.IsAlive
			&& __instance.CombatState != null)
		{
			__result = true;
		}
	}

	private static void PatchTargetingMethod(Harmony harmony, MethodInfo? original)
	{
		if (original == null || CardGetter == null)
		{
			throw new MissingMethodException(
				$"Could not find the STS2 method required by {nameof(ErasureTargeting)}.");
		}

		harmony.Patch(
			original,
			prefix: new HarmonyMethod(
				typeof(ErasureTargeting),
				nameof(TargetingPrefix)),
			postfix: new HarmonyMethod(
				typeof(ErasureTargeting),
				nameof(TargetingPostfix)));
	}
}
