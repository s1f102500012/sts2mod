using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

internal static class EntropyEnchantmentHooks
{
	private const string HarmonyId = "Natsuki.HextechRunesSponsorPack.EntropyEnchantments";
	private static readonly AsyncLocal<int> SuppressCardAddModificationDepth = new();
	private static readonly object InstallLock = new();
	private static Harmony? _harmony;
	private static bool _installed;

	public static void Install()
	{
		lock (InstallLock)
		{
			if (_installed)
			{
				return;
			}

			MethodInfo afterCombatEnd = AccessTools.Method(
				typeof(Hook),
				nameof(Hook.AfterCombatEnd),
				[ typeof(IRunState), typeof(ICombatState), typeof(CombatRoom) ])
				?? throw new MissingMethodException(typeof(Hook).FullName, nameof(Hook.AfterCombatEnd));
			MethodInfo modifyCardBeingAddedToDeck = AccessTools.Method(
				typeof(Hook),
				nameof(Hook.ModifyCardBeingAddedToDeck))
				?? throw new MissingMethodException(typeof(Hook).FullName, nameof(Hook.ModifyCardBeingAddedToDeck));

			Harmony harmony = _harmony ??= new Harmony(HarmonyId);
			try
			{
				harmony.Patch(
					afterCombatEnd,
					prefix: new HarmonyMethod(typeof(EntropyEnchantmentHooks), nameof(AfterCombatEndPrefix)),
					postfix: new HarmonyMethod(typeof(EntropyEnchantmentHooks), nameof(AfterCombatEndPostfix)));
				harmony.Patch(
					modifyCardBeingAddedToDeck,
					prefix: new HarmonyMethod(typeof(EntropyEnchantmentHooks), nameof(ModifyCardBeingAddedToDeckPrefix)));
			}
			catch
			{
				harmony.UnpatchAll(HarmonyId);
				_harmony = null;
				throw;
			}

			_installed = true;
			Log.Info($"[{ModInfo.Id}] Entropy enchantment hooks installed.");
		}
	}

	private static bool ModifyCardBeingAddedToDeckPrefix(
		CardModel card,
		ref CardModel __result,
		ref List<AbstractModel> modifyingModels)
	{
		if (SuppressCardAddModificationDepth.Value <= 0)
		{
			return true;
		}

		__result = card;
		modifyingModels = [];
		return false;
	}

	internal static bool IsTransformingEntropyCard => SuppressCardAddModificationDepth.Value > 0;

	internal static async Task<T> TransformWithoutDeckModification<T>(Func<Task<T>> transform)
	{
		ArgumentNullException.ThrowIfNull(transform);
		SuppressCardAddModificationDepth.Value++;
		try
		{
			return await transform();
		}
		finally
		{
			SuppressCardAddModificationDepth.Value--;
		}
	}

	private static void AfterCombatEndPrefix(
		IRunState runState,
		ICombatState combatState,
		out IReadOnlyList<CardModel> __state)
	{
		__state = runState.IterateHookListeners(combatState)
			.OfType<EnchantmentModel>()
			.Select(static enchantment => EnchantmentCompositionAdapter.Find(enchantment, typeof(EntropyDecrease)) as EntropyDecrease)
			.Where(static enchantment => enchantment?.PendingRemoval == true)
			.Select(static enchantment => enchantment!.Card.DeckVersion ?? enchantment.Card)
			.Distinct()
			.ToArray();
	}

	private static void AfterCombatEndPostfix(
		IReadOnlyList<CardModel>? __state,
		ref Task __result)
	{
		if (__state is { Count: > 0 })
		{
			__result = RemoveEntropyDecreaseCards(__result, __state);
		}
	}

	private static async Task RemoveEntropyDecreaseCards(
		Task originalTask,
		IReadOnlyList<CardModel> pendingCards)
	{
		await originalTask;
		IReadOnlyList<CardModel> cardsToRemove = pendingCards
			.Where(static card =>
				card.Pile?.Type == PileType.Deck
				&& (EnchantmentCompositionAdapter.Find(card, typeof(EntropyDecrease)) as EntropyDecrease)?.PendingRemoval == true)
			.ToArray();
		if (cardsToRemove.Count == 0)
		{
			return;
		}

		foreach (CardModel card in cardsToRemove)
		{
			Log.Info($"[{ModInfo.Id}][EntropyDecrease] Removing {card.Id.Entry} from the deck after combat.");
		}
		await CardPileCmd.RemoveFromDeck(cardsToRemove, showPreview: true);
	}
}
