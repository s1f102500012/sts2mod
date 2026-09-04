using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal static class AiTeammateCardSelectionPatches
{
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
    private static class CardSelectChooseACardPatch
    {
        private static bool Prefix(
            PlayerChoiceContext context,
            IReadOnlyList<CardModel> cards,
            Player player,
            bool canSkip,
            ref Task<CardModel?> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            __result = AiTeammateDummyController.ChooseFirstCardFromChooseScreenAsync(context, cards, player, canSkip);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGridForRewards))]
    private static class CardSelectSimpleGridRewardsPatch
    {
        private static bool Prefix(
            PlayerChoiceContext context,
            List<CardCreationResult> cards,
            Player player,
            CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                context,
                cards.Select(static card => card.Card),
                prefs.MinSelect,
                prefs.MaxSelect,
                selectionMode: DeterministicCardSelectionMode.BestCandidate,
                player: player);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGrid))]
    private static class CardSelectSimpleGridPatch
    {
        private static bool Prefix(
            PlayerChoiceContext context,
            IReadOnlyList<CardModel> cardsIn,
            Player player,
            CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            if (TryPrepareSafeHandCleanupSelection(player, cardsIn, prefs, out IReadOnlyList<CardModel> safeHandTargets))
            {
                __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                    context,
                    safeHandTargets,
                    prefs.MinSelect,
                    prefs.MaxSelect,
                    PlayerChoiceOptions.CancelPlayCardActions,
                    selectionMode: DeterministicCardSelectionMode.First,
                    player: player);
                return false;
            }

            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                context,
                cardsIn,
                prefs.MinSelect,
                prefs.MaxSelect,
                selectionMode: IsTransformSelection(prefs)
                    ? DeterministicCardSelectionMode.WorstForRemovalOrTransform
                    : DeterministicCardSelectionMode.BestCandidate,
                player: player);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForUpgrade))]
    private static class CardSelectDeckUpgradePatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            IEnumerable<CardModel> options = PileType.Deck.GetPile(player).Cards.Where(static card => card.IsUpgradable);
            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                null,
                options,
                prefs.MinSelect,
                prefs.MaxSelect,
                selectionMode: DeterministicCardSelectionMode.BestUpgrade,
                player: player);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForTransformation))]
    private static class CardSelectDeckTransformPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            IEnumerable<CardModel> options = PileType.Deck.GetPile(player).Cards.Where(static card => card.Type != CardType.Quest && card.IsTransformable);
            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                null,
                options,
                prefs.MinSelect,
                prefs.MaxSelect,
                selectionMode: DeterministicCardSelectionMode.WorstForRemovalOrTransform,
                player: player);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForEnchantment), new[] { typeof(IReadOnlyList<CardModel>), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) })]
    private static class CardSelectDeckEnchantmentPatch
    {
        private static bool Prefix(
            IReadOnlyList<CardModel> cards,
            EnchantmentModel enchantment,
            int amount,
            CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            Player? player = cards.FirstOrDefault()?.Owner;
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            IEnumerable<CardModel> options = cards.Where(enchantment.CanEnchant);
            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                null,
                options,
                prefs.MinSelect,
                prefs.MaxSelect,
                selectionMode: DeterministicCardSelectionMode.BestEnchantTarget,
                player: player);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckGeneric))]
    private static class CardSelectDeckGenericPatch
    {
        private static bool Prefix(
            Player player,
            CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter,
            Func<CardModel, int>? sortingOrder,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            IEnumerable<CardModel> options = PileType.Deck.GetPile(player).Cards;
            if (filter != null)
            {
                options = options.Where(filter);
            }

            if (sortingOrder != null)
            {
                options = options.OrderBy(sortingOrder);
            }

            bool isRemoveSelection = string.Equals(prefs.Prompt.LocEntryKey, CardSelectorPrefs.RemoveSelectionPrompt.LocEntryKey, StringComparison.Ordinal);
            bool isTransformSelection = IsTransformSelection(prefs);
            if (isRemoveSelection &&
                AiTeammateDummyController.TryConsumePendingShopRemovalSelection(player, options, out IEnumerable<CardModel> selectedRemovalCards))
            {
                __result = Task.FromResult(selectedRemovalCards);
                return false;
            }

            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                null,
                options,
                prefs.MinSelect,
                prefs.MaxSelect,
                selectionMode: isRemoveSelection || isTransformSelection
                    ? DeterministicCardSelectionMode.WorstForRemovalOrTransform
                    : DeterministicCardSelectionMode.BestCandidate,
                player: player);
            return false;
        }
    }

    private static bool IsTransformSelection(CardSelectorPrefs prefs)
    {
        string promptKey = prefs.Prompt.LocEntryKey;
        return string.Equals(promptKey, CardSelectorPrefs.TransformSelectionPrompt.LocEntryKey, StringComparison.Ordinal) ||
               promptKey.Contains("Transform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryPrepareSafeHandCleanupSelection(
        Player player,
        IEnumerable<CardModel> rawOptions,
        CardSelectorPrefs prefs,
        out IReadOnlyList<CardModel> safeTargets)
    {
        List<CardModel> options = rawOptions.ToList();
        safeTargets = [];
        if (!IsCurrentHandOptionSet(player, options))
        {
            return false;
        }

        bool hasNegativeStatusOrCurse = options.Any(card => StatusCardStrategy.IsNegativeStatusOrCurse(card));
        if (!hasNegativeStatusOrCurse && !IsLikelyHandCleanupPrompt(prefs))
        {
            return false;
        }

        safeTargets = PrepareSafeHandCleanupTargets(player, options, "FromSimpleGrid");
        return true;
    }

    private static IReadOnlyList<CardModel> PrepareSafeHandCleanupTargets(
        Player player,
        IEnumerable<CardModel> rawOptions,
        string source)
    {
        List<CardModel> safeCleanupTargets = StatusCardStrategy
            .RankHandCleanupTargets(rawOptions, player)
            .ToList();
        if (safeCleanupTargets.Count <= 0)
        {
            Log.Info($"[AITeammate] No safe hand cleanup target source={source} player={player.NetId}; returning empty hand selection to avoid exhausting/discarding useful cards.");
            return [];
        }

        Log.Info($"[AITeammate] Safe hand cleanup targets source={source} player={player.NetId} cards={string.Join(",", safeCleanupTargets.Select(static card => card.Id.Entry))}");
        return safeCleanupTargets;
    }

    private static bool IsCurrentHandOptionSet(Player player, IReadOnlyList<CardModel> options)
    {
        if (options.Count <= 0)
        {
            return false;
        }

        IReadOnlyList<CardModel> handCards = PileType.Hand.GetPile(player).Cards;
        return options.All(option => handCards.Any(handCard =>
            ReferenceEquals(handCard, option) ||
            (ReferenceEquals(option.Owner, player) &&
             string.Equals(handCard.Id.Entry, option.Id.Entry, StringComparison.Ordinal))));
    }

    private static bool IsLikelyHandCleanupPrompt(CardSelectorPrefs prefs)
    {
        string promptKey = prefs.Prompt.LocEntryKey;
        return promptKey.Contains("Exhaust", StringComparison.OrdinalIgnoreCase) ||
               promptKey.Contains("Discard", StringComparison.OrdinalIgnoreCase) ||
               promptKey.Contains("Transform", StringComparison.OrdinalIgnoreCase) ||
               promptKey.Contains("Remove", StringComparison.OrdinalIgnoreCase) ||
               promptKey.Contains("Purge", StringComparison.OrdinalIgnoreCase) ||
               promptKey.Contains("消耗", StringComparison.Ordinal) ||
               promptKey.Contains("弃", StringComparison.Ordinal) ||
               promptKey.Contains("變", StringComparison.Ordinal) ||
               promptKey.Contains("变", StringComparison.Ordinal);
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand))]
    private static class CardSelectHandPatch
    {
        private static bool Prefix(
            PlayerChoiceContext context,
            Player player,
            CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            IEnumerable<CardModel> options = PileType.Hand.GetPile(player).Cards;
            if (filter != null)
            {
                options = options.Where(filter);
            }

            options = PrepareSafeHandCleanupTargets(player, options, "FromHand");

            __result = AiTeammateDummyController.ChooseDeterministicCardsAsync(
                context,
                options,
                prefs.MinSelect,
                prefs.MaxSelect,
                PlayerChoiceOptions.CancelPlayCardActions,
                selectionMode: DeterministicCardSelectionMode.First,
                player: player);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForUpgrade))]
    private static class CardSelectHandUpgradePatch
    {
        private static bool Prefix(
            PlayerChoiceContext context,
            Player player,
            AbstractModel source,
            ref Task<CardModel?> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            __result = ChooseHandUpgradeAsync(context, player);
            return false;
        }

        private static async Task<CardModel?> ChooseHandUpgradeAsync(PlayerChoiceContext context, Player player)
        {
            IEnumerable<CardModel> selected = await AiTeammateDummyController.ChooseDeterministicCardsAsync(
                context,
                PileType.Hand.GetPile(player).Cards.Where(static card => card.IsUpgradable),
                1,
                1,
                PlayerChoiceOptions.CancelPlayCardActions,
                selectionMode: DeterministicCardSelectionMode.BestUpgrade,
                player: player);
            return selected.FirstOrDefault();
        }
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseABundleScreen))]
    private static class CardSelectBundlePatch
    {
        private static bool Prefix(
            Player player,
            IReadOnlyList<IReadOnlyList<CardModel>> bundles,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            __result = Task.FromResult<IEnumerable<CardModel>>(AiTeammateDummyController.ChooseBestBundle(player, bundles));
            return false;
        }
    }
}
