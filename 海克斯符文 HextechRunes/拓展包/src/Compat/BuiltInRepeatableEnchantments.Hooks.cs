using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

internal static partial class BuiltInRepeatableEnchantments
{
	private static void InstallHookListenerExpansion(Harmony harmony)
	{
		MethodInfo runListeners = RequireMethod(
			typeof(RunState),
			nameof(RunState.IterateHookListeners),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(ICombatState));
		MethodInfo combatListeners = RequireMethod(
			typeof(CombatState),
			nameof(CombatState.IterateHookListeners),
			BindingFlags.Instance | BindingFlags.Public);
		MethodInfo goopyAfterCardPlayed = RequireMethod(
			typeof(Goopy),
			nameof(Goopy.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(PlayerChoiceContext),
			typeof(CardPlay));

		harmony.Patch(
			runListeners,
			postfix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(ExpandHookListenersPostfix)));
		harmony.Patch(
			combatListeners,
			postfix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(ExpandHookListenersPostfix)));
		harmony.Patch(
			goopyAfterCardPlayed,
			prefix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(GoopyAfterCardPlayedPrefix)));
	}

	private static void ExpandHookListenersPostfix(ref IEnumerable<AbstractModel> __result)
	{
		__result = ExpandCompositeHookListeners(__result);
	}

	internal static IEnumerable<AbstractModel> ExpandCompositeHookListeners(IEnumerable<AbstractModel> listeners)
	{
		foreach (AbstractModel listener in listeners)
		{
			if (listener is SponsorCompositeEnchantment composite)
			{
				foreach (EnchantmentModel inner in composite.InnerEnchantments)
				{
					yield return inner;
				}
				continue;
			}

			yield return listener;
		}
	}

	private static bool GoopyAfterCardPlayedPrefix(Goopy __instance, CardPlay cardPlay, ref Task __result)
	{
		if (cardPlay.Card != __instance.Card
			|| __instance.Card.DeckVersion?.Enchantment is not SponsorCompositeEnchantment deckComposite
			|| deckComposite.FindEnchantment(typeof(Goopy)) is not Goopy deckGoopy)
		{
			return true;
		}

		__instance.Amount++;
		deckGoopy.Amount++;
		__result = Task.CompletedTask;
		return false;
	}
}
