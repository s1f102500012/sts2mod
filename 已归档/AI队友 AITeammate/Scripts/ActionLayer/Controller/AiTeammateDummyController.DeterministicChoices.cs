using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;

namespace AITeammate.Scripts;

internal enum DeterministicCardSelectionMode
{
    First,
    BestCandidate,
    BestUpgrade,
    WorstForRemovalOrTransform,
    BestEnchantTarget
}

internal sealed partial class AiTeammateDummyController
{
    private static readonly FieldInfo? CardRewardCardsField =
        typeof(CardReward).GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly CardChoiceEvaluator CardEvaluator = new();

    public static async Task ExecuteDeterministicRewardSetAsync(RewardsSet rewardsSet)
    {
        using IDisposable selectorScope = PushDeterministicCardSelector();
        Log.Info($"[AITeammate] Deterministic reward set start player={rewardsSet.Player.NetId} room={rewardsSet.Room?.GetType().Name ?? "Custom"} roomCount={rewardsSet.Player.RunState.CurrentRoomCount} currentRoom={rewardsSet.Player.RunState.CurrentRoom?.GetType().Name ?? "null"}");
        await rewardsSet.GenerateWithoutOffering();
        foreach (Reward reward in rewardsSet.Rewards.ToList())
        {
            Log.Info($"[AITeammate] Deterministic reward executing player={rewardsSet.Player.NetId} reward={reward.GetType().Name} roomCount={rewardsSet.Player.RunState.CurrentRoomCount} currentRoom={rewardsSet.Player.RunState.CurrentRoom?.GetType().Name ?? "null"}");
            await ExecuteRewardAsync(reward);
        }
        Log.Info($"[AITeammate] Deterministic reward set complete player={rewardsSet.Player.NetId} room={rewardsSet.Room?.GetType().Name ?? "Custom"} roomCount={rewardsSet.Player.RunState.CurrentRoomCount} currentRoom={rewardsSet.Player.RunState.CurrentRoom?.GetType().Name ?? "null"}");
    }

    public static async Task ExecuteDeterministicLocalRewardSetAsync(RewardsSet rewardsSet)
    {
        await ExecuteDeterministicRewardSetAsync(rewardsSet);
    }

    public static async Task ProceedAfterDeterministicTerminalRewardsAsync(Player player, CombatRoom room)
    {
        if (!Hook.ShouldProceedToNextMapPoint(player.RunState))
        {
            Log.Warn($"[AITeammate] Host autopilot cannot proceed after terminal rewards yet player={player.NetId} room={room.GetType().Name} roomType={room.RoomType}");
            return;
        }

        if (room.RoomType == RoomType.Boss || room.IsVictoryRoom)
        {
            if (player.RunState.Map.SecondBossMapPoint != null &&
                player.RunState.CurrentMapCoord == player.RunState.Map.BossMapPoint.coord)
            {
                Log.Info($"[AITeammate] Host autopilot proceeding from second boss terminal rewards player={player.NetId}");
                await RunManager.Instance.ProceedFromTerminalRewardsScreen();
                return;
            }

            Log.Info($"[AITeammate] Host autopilot readying for act transition player={player.NetId} roomType={room.RoomType}");
            RunManager.Instance.ActChangeSynchronizer.SetLocalPlayerReady();
            return;
        }

        Log.Info($"[AITeammate] Host autopilot proceeding from terminal rewards player={player.NetId} roomType={room.RoomType}");
        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
    }

    public static async Task<bool> ExecuteDeterministicCardRewardAsync(CardReward reward)
    {
        List<CardCreationResult> cards = GetCardRewardCards(reward);
        ulong historyNetId = reward.Player.NetId;
        var historyEntry = reward.Player.RunState.CurrentMapPointHistoryEntry;
        if (historyEntry == null)
        {
            return false;
        }

        FutureRewardRouteEvaluation futureRewards = reward.Player.RunState is RunState runState
            ? FutureRewardOracle.Shared.EvaluateBestReachableFromCurrentMap(
                runState,
                reward.Player,
                lookaheadDepth: 4)
            : FutureRewardRouteEvaluation.Empty;
        if (futureRewards.CardRewards.Count > 0)
        {
            Log.Info($"[AITeammate] Future reward context player={reward.Player.NetId} source=card_reward {futureRewards.Describe()}");
        }

        CardChoiceDecision decision = CardEvaluator.EvaluateCandidates(
            cards.Select(static card => card.Card),
            CardEvaluator.ContextFactory.Create(
                reward.Player,
                CardChoiceSource.Reward,
                reward.CanSkip,
                debugSource: "card_reward",
                futureRewards: futureRewards));
        LogCardChoiceDecision(reward.Player, decision, "reward");

        CardModel? selected = decision.ShouldTakeCard
            ? decision.BestEvaluation?.CandidateCard
            : null;
        if (selected != null)
        {
            CardPileAddResult addResult = await CardPileCmd.Add(selected, PileType.Deck);
            if (addResult.success)
            {
                CardModel addedCard = addResult.cardAdded;
                historyEntry
                    .GetEntry(historyNetId)
                    .CardChoices.Add(new CardChoiceHistoryEntry(addedCard, wasPicked: true));
                RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(addedCard);
                cards.RemoveAll(card => card.Card == selected);
                Log.Info($"[AITeammate] Deterministic card reward picked player={reward.Player.NetId} card={addedCard.Id.Entry}");
            }
        }
        else
        {
            Log.Info($"[AITeammate] Deterministic card reward skipped player={reward.Player.NetId} threshold={decision.SkipThreshold:F1}");
        }

        foreach (CardCreationResult card in cards)
        {
            historyEntry
                .GetEntry(historyNetId)
                .CardChoices.Add(new CardChoiceHistoryEntry(card.Card, wasPicked: false));
            RunManager.Instance.RewardSynchronizer.SyncLocalSkippedCard(card.Card);
        }

        return false;
    }

    public static Task<CardModel?> ChooseFirstCardFromChooseScreenAsync(
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        Player player,
        bool canSkip)
    {
        CardChoiceSource source = canSkip ? CardChoiceSource.ChooseScreen : CardChoiceSource.ForcedChoice;
        CardChoiceDecision decision = CardEvaluator.EvaluateCandidates(
            cards,
            CardEvaluator.ContextFactory.Create(
                player,
                source,
                canSkip,
                debugSource: "choose_a_card"));
        LogCardChoiceDecision(player, decision, "choose_screen");
        CardModel? selected = decision.ShouldTakeCard
            ? decision.BestEvaluation?.CandidateCard
            : null;
        return Task.FromResult(selected);
    }

    public static Task<IEnumerable<CardModel>> ChooseDeterministicCardsAsync(
        PlayerChoiceContext? context,
        IEnumerable<CardModel> options,
        int minSelect,
        int maxSelect,
        PlayerChoiceOptions choiceOptions = PlayerChoiceOptions.None,
        DeterministicCardSelectionMode selectionMode = DeterministicCardSelectionMode.First,
        Player? player = null)
    {
        List<CardModel> list = options.ToList();
        Player? selectionPlayer = player ?? list.FirstOrDefault()?.Owner;
        if (selectionMode == DeterministicCardSelectionMode.WorstForRemovalOrTransform &&
            selectionPlayer != null)
        {
            list = FilterSafeRemovalOrTransformOptions(list, selectionPlayer);
        }

        int desiredCount = ComputeSelectionCount(list.Count, minSelect, maxSelect);
        IEnumerable<CardModel> ranked = RankCardsForSelection(list, selectionMode, selectionPlayer);
        IEnumerable<CardModel> selected = ranked.Take(desiredCount).ToList();

        return Task.FromResult(selected);
    }

    public static RelicModel? ChooseFirstRelic(IReadOnlyList<RelicModel> relics)
    {
        return relics.FirstOrDefault();
    }

    public static IReadOnlyList<CardModel> ChooseFirstBundle(IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    {
        return bundles.FirstOrDefault() ?? Array.Empty<CardModel>();
    }

    public static IReadOnlyList<CardModel> ChooseBestBundle(Player player, IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    {
        return bundles
            .OrderByDescending(bundle => bundle.Sum(card => ScoreCardStandalone(card, player)))
            .ThenBy(bundle => string.Join("|", bundle.Select(static card => card.Id.Entry)), StringComparer.Ordinal)
            .FirstOrDefault()
            ?? Array.Empty<CardModel>();
    }

    public static IDisposable PushDeterministicCardSelector()
    {
        var selector = new DeterministicCardSelector();
        return CardSelectCmd.Selector == null
            ? CardSelectCmd.UseSelector(selector)
            : CardSelectCmd.PushSelector(selector);
    }

    private static int ComputeSelectionCount(int optionCount, int minSelect, int maxSelect)
    {
        if (optionCount <= 0 || maxSelect <= 0)
        {
            return 0;
        }

        int desiredCount = minSelect > 0 ? minSelect : 1;
        desiredCount = Math.Min(desiredCount, optionCount);
        desiredCount = Math.Min(desiredCount, maxSelect);
        return Math.Max(desiredCount, 0);
    }

    private static IEnumerable<CardModel> RankCardsForSelection(
        IReadOnlyList<CardModel> options,
        DeterministicCardSelectionMode selectionMode,
        Player? player)
    {
        return selectionMode switch
        {
            DeterministicCardSelectionMode.BestCandidate => RankBestCandidates(options, player),
            DeterministicCardSelectionMode.BestUpgrade => options
                .OrderByDescending(card => ScoreUpgradeCandidate(card, player))
                .ThenBy(static card => card.Title?.ToString() ?? card.Id.Entry, StringComparer.Ordinal),
            DeterministicCardSelectionMode.WorstForRemovalOrTransform => options
                .OrderByDescending(card => ScoreRemovalOrTransformBurden(card, player))
                .ThenBy(static card => card.Title?.ToString() ?? card.Id.Entry, StringComparer.Ordinal),
            DeterministicCardSelectionMode.BestEnchantTarget => options
                .OrderByDescending(card => ScoreEnchantTarget(card, player))
                .ThenBy(static card => card.Title?.ToString() ?? card.Id.Entry, StringComparer.Ordinal),
            _ => options
        };
    }

    private static IEnumerable<CardModel> RankBestCandidates(IReadOnlyList<CardModel> options, Player? player)
    {
        if (player != null)
        {
            CardChoiceDecision decision = CardEvaluator.EvaluateCandidates(
                options,
                CardEvaluator.ContextFactory.Create(
                    player,
                    CardChoiceSource.ForcedChoice,
                    skipAllowed: false,
                    debugSource: "deterministic_multi_select"));
            return decision.RankedResults.Select(static result => result.CandidateCard).ToList();
        }

        return options
            .OrderByDescending(card => ScoreCardStandalone(card, player))
            .ThenBy(static card => card.Title?.ToString() ?? card.Id.Entry, StringComparer.Ordinal)
            .ToList();
    }

    private static double ScoreUpgradeCandidate(CardModel card, Player? player)
    {
        double baseScore = ScoreCardStandalone(card, player);
        if (!card.IsUpgradable)
        {
            return baseScore - 1000d;
        }

        try
        {
            CardModel upgraded = card.ToMutable();
            upgraded.UpgradeInternal();
            upgraded.FinalizeUpgradeInternal();
            double upgradedScore = ScoreCardStandalone(upgraded, player);
            double upgradeDelta = upgradedScore - baseScore;
            string normalizedId = NormalizeCardToken(card.Id.Entry);
            if (card.Type == CardType.Attack && card.Owner?.RunState.TotalFloor <= 10)
            {
                upgradeDelta += 3d;
            }

            if (card.Type == CardType.Power)
            {
                upgradeDelta += 1.5d;
            }

            if (IsPriorityUpgrade(normalizedId))
            {
                upgradeDelta += 5d;
            }

            if (normalizedId.Contains("STRIKE", StringComparison.Ordinal) ||
                normalizedId.Contains("DEFEND", StringComparison.Ordinal))
            {
                upgradeDelta -= 4d;
            }

            return upgradeDelta + Math.Max(baseScore, 0d) * 0.05d;
        }
        catch (Exception exception)
        {
            double fallbackScore = EstimateUpgradeCandidateFallback(card, player, baseScore);
            Log.Debug($"[AITeammate] Fallback upgrade value for card={card.Id.Entry}: {exception.Message}; score={fallbackScore:F1}");
            return fallbackScore;
        }
    }

    private static double EstimateUpgradeCandidateFallback(CardModel card, Player? player, double baseScore)
    {
        string normalizedId = NormalizeCardToken(card.Id.Entry);
        double score = Math.Max(baseScore, 0d) * 0.08d + 4d;
        if (card.Type == CardType.Attack && card.Owner?.RunState.TotalFloor <= 10)
        {
            score += 3d;
        }

        if (card.Type == CardType.Power)
        {
            score += 2.5d;
        }

        if (IsPriorityUpgrade(normalizedId))
        {
            score += 5d;
        }

        if (ContainsAny(
                normalizedId,
                "ARMAMENTS",
                "THE_SMITH",
                "SHOCKWAVE",
                "PIERCING_WAIL",
                "BASH",
                "ZAP",
                "DUALCAST",
                "LOOP",
                "FEEL_NO_PAIN"))
        {
            score += 4d;
        }

        if (normalizedId.Contains("STRIKE", StringComparison.Ordinal) ||
            normalizedId.Contains("DEFEND", StringComparison.Ordinal))
        {
            score -= 4d;
        }

        return score;
    }

    private static double ScoreEnchantTarget(CardModel card, Player? player)
    {
        double score = ScoreCardStandalone(card, player);
        if (card.Type == CardType.Power)
        {
            score += 5d;
        }

        if (card.IsUpgraded)
        {
            score += 2d;
        }

        if (card.Rarity.ToString() is "Rare" or "Uncommon")
        {
            score += 2d;
        }

        return score;
    }

    private static double ScoreRemovalOrTransformBurden(CardModel card, Player? player)
    {
        ResolvedCardView resolved = CardEvaluator.ContextFactory.ResolveCandidate(card, 0);
        double burden = 0d;
        string cardId = card.Id.Entry;

        burden += resolved.Rarity switch
        {
            "Curse" => 60d,
            "Status" => 42d,
            "Basic" => 12d,
            "Rare" => -8d,
            "Uncommon" => -3d,
            _ => 0d
        };

        if (cardId.Contains("STRIKE", StringComparison.OrdinalIgnoreCase))
        {
            burden += 18d;
        }

        if (cardId.Contains("DEFEND", StringComparison.OrdinalIgnoreCase))
        {
            burden += 14d;
        }

        double output = ScoreResolvedCard(resolved);
        burden -= output * 0.35d;
        burden += StatusCardStrategy.GetBurdenScore(resolved);
        if (output <= 8d)
        {
            burden += 8d;
        }

        if (resolved.EffectiveCost >= 2 && output <= 15d)
        {
            burden += 6d;
        }

        if (resolved.IsUpgraded)
        {
            burden -= 8d;
        }

        if (resolved.Type == CardType.Power)
        {
            burden -= 4d;
        }

        burden += RegentCharacterStrategy.ScoreRemovalOrTransformBurden(resolved, player);
        if (player != null)
        {
            IReadOnlyList<ResolvedCardView> deckCards = CardEvaluator.ContextFactory.Create(
                player,
                CardChoiceSource.ForcedChoice,
                skipAllowed: false,
                debugSource: "removal_safety").DeckCards;
            burden = CardRemovalSafetyPolicy.ApplyDeckRoleProtection(
                resolved,
                deckCards,
                player,
                burden,
                []);
        }

        return burden;
    }

    private static List<CardModel> FilterSafeRemovalOrTransformOptions(IReadOnlyList<CardModel> options, Player player)
    {
        IReadOnlyList<ResolvedCardView> deckCards = CardEvaluator.ContextFactory.Create(
            player,
            CardChoiceSource.ForcedChoice,
            skipAllowed: false,
            debugSource: "removal_transform_safety").DeckCards;
        List<CardModel> safeOptions = [];
        foreach (CardModel option in options)
        {
            ResolvedCardView resolved = CardEvaluator.ContextFactory.ResolveCandidate(option, 0);
            if (!CardRemovalSafetyPolicy.CanRemoveFromDeck(resolved, deckCards, out string reason))
            {
                Log.Info($"[AITeammate] Skipping unsafe removal/transform target player={player.NetId} card={option.Id.Entry} reason={reason}");
                continue;
            }

            double burden = ScoreRemovalOrTransformBurden(option, player);
            if (!CardRemovalSafetyPolicy.IsWorthwhileRemovalBurden(burden))
            {
                Log.Info($"[AITeammate] Skipping low-value removal/transform target player={player.NetId} card={option.Id.Entry} burden={burden:F1}");
                continue;
            }

            safeOptions.Add(option);
        }

        return safeOptions;
    }

    private static double ScoreCardStandalone(CardModel card, Player? player)
    {
        ResolvedCardView resolved = CardEvaluator.ContextFactory.ResolveCandidate(card, 0);
        double score = ScoreResolvedCard(resolved);
        score += RegentCharacterStrategy.ScoreStandaloneCard(resolved, player);
        if (player != null)
        {
            DeckSummary deck = CardEvaluator.ContextFactory.Create(
                player,
                CardChoiceSource.ForcedChoice,
                skipAllowed: false,
                debugSource: "standalone_card_score").DeckSummary;
            if (deck.FrontloadDamageSources < 6 && resolved.GetEstimatedDamage() > 0)
            {
                score += 4d;
            }

            if (deck.BlockSources < 5 && resolved.GetEstimatedProtection() > 0)
            {
                score += 4d;
            }

            if (deck.DrawSources < 2 && resolved.GetCardsDrawn() > 0)
            {
                score += 5d;
            }

            if (deck.EnergySources < 1 && resolved.GetEnergyGain() > 0)
            {
                score += 6d;
            }

            if (deck.AoESources == 0 && resolved.DealsDamageToAllEnemies())
            {
                score += player.RunState.CurrentActIndex <= 1 ? 8d : 4d;
            }

            if (deck.ScalingSources < 2 &&
                (resolved.GetSelfStrengthAmount() > 0 ||
                 resolved.GetSelfDexterityAmount() > 0 ||
                 resolved.Type == CardType.Power) &&
                player.RunState.ActFloor >= 8)
            {
                score += 5d;
            }

            if (deck.BadCards >= 2 && (resolved.GetCardsDrawn() > 0 || resolved.Exhaust))
            {
                score += Math.Min(deck.BadCards, 5) * 1.5d;
            }

            if (player.RunState.CurrentActIndex == 0 &&
                deck.ControlledHandCleanupCards <= 0 &&
                StatusCardStrategy.IsControlledHandCleanupCard(resolved))
            {
                score += player.RunState.ActFloor <= 8 ? 18d : 10d;
            }
        }

        return score;
    }

    private static double ScoreResolvedCard(ResolvedCardView card)
    {
        double score = 0d;
        score += card.GetEstimatedDamage() * 0.85d;
        score += card.GetEstimatedBlock() * 0.75d;
        score += card.GetSummonAmount() * 1.05d;
        score += card.GetCardsDrawn() * 7d;
        score += card.GetEnergyGain() * 10d;
        score += card.GetEnemyVulnerableAmount() * 5d;
        score += card.GetEnemyWeakAmount() * 5.5d;
        score += card.GetSelfStrengthAmount() * 5d;
        score += card.GetSelfDexterityAmount() * 5d;
        score += card.IsMultiplayerOnlyCard() ? 9d : 0d;
        score += card.Type == CardType.Power ? 3d : 0d;
        score += card.Rarity switch
        {
            "Rare" => 3d,
            "Uncommon" => 1.5d,
            "Basic" => -4d,
            "Curse" => -50d,
            "Status" => -30d,
            _ => 0d
        };

        if (card.EffectiveCost == 0)
        {
            score += 3d;
        }
        else if (card.EffectiveCost >= 2)
        {
            score -= (card.EffectiveCost - 1) * 2d;
        }

        if (card.Exhaust && score < 16d)
        {
            score -= 2d;
        }

        if (card.Ethereal)
        {
            score -= 5d;
        }

        if (card.Retain)
        {
            score += 2d;
        }

        return score;
    }

    private static bool IsPriorityUpgrade(string normalizedCardId)
    {
        return normalizedCardId.Contains("BASH", StringComparison.Ordinal) ||
               normalizedCardId.Contains("SHRUG", StringComparison.Ordinal) ||
               normalizedCardId.Contains("POMMEL", StringComparison.Ordinal) ||
               normalizedCardId.Contains("UPPERCUT", StringComparison.Ordinal) ||
               normalizedCardId.Contains("FLAME_BARRIER", StringComparison.Ordinal) ||
               normalizedCardId.Contains("FEED", StringComparison.Ordinal) ||
               normalizedCardId.Contains("OFFERING", StringComparison.Ordinal) ||
               normalizedCardId.Contains("WHIRLWIND", StringComparison.Ordinal) ||
               normalizedCardId.Contains("CORRUPTION", StringComparison.Ordinal) ||
               normalizedCardId.Contains("DARK_EMBRACE", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string normalizedCardId, params string[] tokens)
    {
        return tokens.Any(token => normalizedCardId.Contains(NormalizeCardToken(token), StringComparison.Ordinal));
    }

    private static string NormalizeCardToken(string value)
    {
        return value.Replace(' ', '_').Replace('-', '_').Replace(':', '_').Replace('/', '_').ToUpperInvariant();
    }

    private static async Task ExecuteRewardAsync(Reward reward)
    {
        switch (reward)
        {
            case CardReward cardReward:
                await ExecuteDeterministicCardRewardAsync(cardReward);
                return;
            case PotionReward potionReward:
                if (await potionReward.OnSelectWrapper())
                {
                    return;
                }

                PotionModel? incomingPotion = potionReward.Potion;
                if (incomingPotion != null &&
                    PotionHeuristicEvaluator.TryChoosePotionToReplace(
                        potionReward.Player,
                        incomingPotion,
                        out PotionModel? currentPotion,
                        out double incomingScore,
                        out double discardScore) &&
                    currentPotion != null)
                {
                    Log.Info($"[AITeammate] Potion reward replacement player={potionReward.Player.NetId} discard={currentPotion.Id.Entry} discardScore={discardScore:F1} incoming={incomingPotion.Id.Entry} incomingScore={incomingScore:F1}");
                    await PotionCmd.Discard(currentPotion);
                    await potionReward.OnSelectWrapper();
                }
                else if (incomingPotion != null)
                {
                    Log.Info($"[AITeammate] Potion reward skipped replacement player={potionReward.Player.NetId} incoming={incomingPotion.Id.Entry}");
                }

                return;
            default:
                await reward.OnSelectWrapper();
                return;
        }
    }

    private static List<CardCreationResult> GetCardRewardCards(CardReward reward)
    {
        return CardRewardCardsField?.GetValue(reward) as List<CardCreationResult> ?? [];
    }

    private static void LogCardChoiceDecision(Player player, CardChoiceDecision decision, string source)
    {
        Log.Info($"[AITeammate] Card evaluation player={player.NetId} source={source} {decision.Describe()}");
        foreach (CardEvaluationResult result in decision.RankedResults.Take(3))
        {
            string reasons = result.Reasons.Count > 0
                ? string.Join(", ", result.Reasons)
                : "no_reasons";
            Log.Info($"[AITeammate] Card evaluation rank player={player.NetId} source={source} {result.Describe()} reasons=[{reasons}]");
        }
    }

    private sealed class DeterministicCardSelector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            List<CardModel> list = options.ToList();
            int selectionCount = ComputeSelectionCount(list.Count, minSelect, maxSelect);
            IEnumerable<CardModel> selected = list.Take(selectionCount).ToList();
            return Task.FromResult(selected);
        }

        public CardModel? GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
        {
            return options.FirstOrDefault()?.Card;
        }
    }
}
