using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

internal static class EntropyEnchantmentHooks
{
	private const string HarmonyId = "Natsuki.HextechRunesSponsorPack.EntropyEnchantments";
	private static readonly AsyncLocal<int> SkipNextCardAddModification = new();
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

#if STS2_109_OR_NEWER
			Type[] addCardsParameterTypes =
			[
				typeof(IEnumerable<CardModel>),
				typeof(CardPile),
				typeof(CardPilePosition),
				typeof(AbstractModel),
				typeof(bool),
				typeof(bool)
			];
#else
			Type[] addCardsParameterTypes =
			[
				typeof(IEnumerable<CardModel>),
				typeof(CardPile),
				typeof(CardPilePosition),
				typeof(AbstractModel),
				typeof(bool)
			];
#endif
			MethodInfo addCards = AccessTools.Method(
				typeof(CardPileCmd),
				nameof(CardPileCmd.Add),
				addCardsParameterTypes) ?? throw new MissingMethodException(typeof(CardPileCmd).FullName, nameof(CardPileCmd.Add));
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
					addCards,
					prefix: new HarmonyMethod(typeof(EntropyEnchantmentHooks), nameof(AddCardsPrefix)),
					postfix: new HarmonyMethod(typeof(EntropyEnchantmentHooks), nameof(AddCardsPostfix)));
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
		if (SkipNextCardAddModification.Value <= 0)
		{
			return true;
		}

		SkipNextCardAddModification.Value--;
		__result = card;
		modifyingModels = [];
		return false;
	}

	private static void AddCardsPrefix(CardPile newPile, out IReadOnlyList<CardModel> __state)
	{
		__state = newPile.Type == PileType.Deck
			? newPile.Cards
				.Where(static card => EnchantmentCompositionAdapter.Find(card.Enchantment, typeof(EntropyIncrease)) != null)
				.ToArray()
			: [];
	}

	private static void AddCardsPostfix(
		CardPile newPile,
		IReadOnlyList<CardModel> __state,
		ref Task<IReadOnlyList<CardPileAddResult>> __result)
	{
		if (__state.Count > 0 && newPile.Type == PileType.Deck)
		{
			__result = ResolveEntropyIncrease(__result, newPile, __state);
		}
	}

	private static async Task<IReadOnlyList<CardPileAddResult>> ResolveEntropyIncrease(
		Task<IReadOnlyList<CardPileAddResult>> originalTask,
		CardPile deck,
		IReadOnlyList<CardModel> entropyCards)
	{
		IReadOnlyList<CardPileAddResult> results = await originalTask;
		CardPileAddResult? obtainedResult = results.FirstOrDefault(static result =>
			result.success && result.oldPile == null);
		CardModel? obtainedCard = obtainedResult?.cardAdded;
		if (obtainedCard == null)
		{
			return results;
		}

		foreach (CardModel entropyCard in entropyCards)
		{
			if (entropyCard.Pile != deck
				|| EnchantmentCompositionAdapter.Find(entropyCard.Enchantment, typeof(EntropyIncrease)) == null)
			{
				continue;
			}

			CardModel replacement = entropyCard.Owner.RunState.CloneCard(obtainedCard);
			CardPileAddResult? transformResult;
			SkipNextCardAddModification.Value = 1;
			try
			{
				transformResult = await CardCmd.Transform(
					entropyCard,
					replacement,
					CardPreviewStyle.HorizontalLayout);
			}
			finally
			{
				SkipNextCardAddModification.Value = 0;
			}
			if (transformResult.HasValue && transformResult.Value.success)
			{
				Log.Info(
					$"[{ModInfo.Id}][EntropyIncrease] Transformed {entropyCard.Id.Entry} into "
					+ $"{transformResult.Value.cardAdded.Id.Entry} after obtaining {obtainedCard.Id.Entry}.");
			}
		}

		return results;
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
		IReadOnlyList<CardModel> __state,
		ref Task __result)
	{
		if (__state.Count > 0)
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
				&& (EnchantmentCompositionAdapter.Find(card.Enchantment, typeof(EntropyDecrease)) as EntropyDecrease)?.PendingRemoval == true)
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
