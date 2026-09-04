using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AITeammate.Scripts;

internal sealed class FutureRewardOracle
{
    public static readonly FutureRewardOracle Shared = new();

    private const int DefaultLookaheadDepth = 5;
    private const int CardRewardOptionCount = 3;
    private const float BaseRarityOffset = -0.05f;
    private const float MaxRarityOffset = 0.4f;
    private const double FutureDepthDiscount = 0.88d;

    private readonly CardChoiceEvaluator _cardChoiceEvaluator = new();

    public IReadOnlyDictionary<MapPoint, FutureRewardRouteEvaluation> EvaluateImmediateChoices(
        RunState runState,
        Player player,
        IReadOnlyList<MapPoint> candidates,
        int lookaheadDepth = DefaultLookaheadDepth)
    {
        if (candidates.Count == 0 || lookaheadDepth <= 0)
        {
            return new Dictionary<MapPoint, FutureRewardRouteEvaluation>();
        }

        Dictionary<MapPoint, FutureRewardRouteEvaluation> results = new();
        RewardSimulationState rootState = RewardSimulationState.From(player, runState);
        foreach (MapPoint candidate in candidates)
        {
            try
            {
                results[candidate] = EvaluateBestRoute(
                    runState,
                    player,
                    candidate,
                    rootState.Clone(),
                    lookaheadDepth,
                    depthFromRoot: 0);
            }
            catch (Exception exception)
            {
                Log.Warn($"[AITeammate] Future reward oracle failed for node={FormatPoint(candidate)} type={candidate.PointType}: {exception.Message}");
            }
        }

        return results;
    }

    public FutureRewardRouteEvaluation EvaluateBestReachableFromCurrentMap(
        RunState runState,
        Player player,
        int lookaheadDepth = DefaultLookaheadDepth)
    {
        IReadOnlyList<MapPoint> candidates = GetCurrentCandidates(runState);
        if (candidates.Count == 0)
        {
            return FutureRewardRouteEvaluation.Empty;
        }

        return EvaluateImmediateChoices(runState, player, candidates, lookaheadDepth)
            .Values
            .OrderByDescending(static evaluation => evaluation.RewardValue)
            .FirstOrDefault()
            ?? FutureRewardRouteEvaluation.Empty;
    }

    public FuturePotionRewardPreview PreviewPotionRewardAfterCurrentCombat(Player player, RoomType roomType)
    {
        if (!IsCombatRewardRoom(roomType))
        {
            return FuturePotionRewardPreview.Empty with
            {
                RoomType = roomType
            };
        }

        try
        {
            RewardSimulationState state = RewardSimulationState.From(player, player.RunState as RunState);
            _ = TryGenerateCardReward(player, roomType, state, out _);
            return RollPotionReward(player, roomType, state, depthFromRoot: 0);
        }
        catch (Exception exception)
        {
            Log.Warn($"[AITeammate] Future potion oracle failed roomType={roomType}: {exception.Message}");
            return FuturePotionRewardPreview.Empty with
            {
                RoomType = roomType
            };
        }
    }

    private FutureRewardRouteEvaluation EvaluateBestRoute(
        RunState runState,
        Player player,
        MapPoint point,
        RewardSimulationState state,
        int remainingDepth,
        int depthFromRoot)
    {
        FutureRewardRouteEvaluation current = EvaluatePoint(runState, player, point, state, depthFromRoot);
        if (remainingDepth <= 1 || point.Children.Count == 0)
        {
            return current;
        }

        FutureRewardRouteEvaluation? bestChild = null;
        foreach (MapPoint child in point.Children.Where(child => runState.Map.HasPoint(child.coord)))
        {
            FutureRewardRouteEvaluation childEvaluation = EvaluateBestRoute(
                runState,
                player,
                child,
                state.Clone(),
                remainingDepth - 1,
                depthFromRoot + 1);
            if (bestChild == null || childEvaluation.RewardValue > bestChild.RewardValue)
            {
                bestChild = childEvaluation;
            }
        }

        if (bestChild == null)
        {
            return current;
        }

        return current.WithFuture(bestChild, FutureDepthDiscount);
    }

    private FutureRewardRouteEvaluation EvaluatePoint(
        RunState runState,
        Player player,
        MapPoint point,
        RewardSimulationState state,
        int depthFromRoot)
    {
        List<MapPoint> path = [point];
        return point.PointType switch
        {
            MapPointType.Monster => EvaluateCombatRewardPoint(runState, player, path, RoomType.Monster, state, depthFromRoot, includeRelic: false),
            MapPointType.Elite => EvaluateCombatRewardPoint(runState, player, path, RoomType.Elite, state, depthFromRoot, includeRelic: true),
            MapPointType.Boss => EvaluateCombatRewardPoint(runState, player, path, RoomType.Boss, state, depthFromRoot, includeRelic: false),
            MapPointType.Treasure => EvaluateTreasurePoint(runState, player, path, state, depthFromRoot),
            MapPointType.Shop => EvaluateShopPoint(player, path, state, depthFromRoot),
            MapPointType.Unknown => EvaluateUnknownPoint(runState, player, path, state, depthFromRoot),
            _ => new FutureRewardRouteEvaluation(path, [], [], [], [], [], 0d, 0)
        };
    }

    private FutureRewardRouteEvaluation EvaluateUnknownPoint(
        RunState runState,
        Player player,
        IReadOnlyList<MapPoint> path,
        RewardSimulationState state,
        int depthFromRoot)
    {
        RoomType resolvedRoom = RollUnknownRoomType(runState, state);
        FutureEventPreview unknownPreview = new(
            EventId: "UNKNOWN_MAP_POINT",
            RoomType: resolvedRoom,
            RewardValue: EstimateUnknownRoomStrategicValue(player, resolvedRoom),
            Confidence: FutureOracleConfidence.ExactRoomTypeEstimatedContents,
            DepthFromRoot: depthFromRoot,
            Reason: $"unknown_roll rewards={state.RewardsRng.Counter} shops={state.ShopsRng.Counter} unknown={state.UnknownRng?.Counter ?? -1}");

        FutureRewardRouteEvaluation resolved = resolvedRoom switch
        {
            RoomType.Monster => EvaluateCombatRewardPoint(runState, player, path, RoomType.Monster, state, depthFromRoot, includeRelic: false),
            RoomType.Elite => EvaluateCombatRewardPoint(runState, player, path, RoomType.Elite, state, depthFromRoot, includeRelic: true),
            RoomType.Treasure => EvaluateTreasurePoint(runState, player, path, state, depthFromRoot),
            RoomType.Shop => EvaluateShopPoint(player, path, state, depthFromRoot),
            RoomType.Event => new FutureRewardRouteEvaluation(
                path,
                [],
                [],
                [],
                [],
                [unknownPreview],
                unknownPreview.RewardValue * Math.Pow(FutureDepthDiscount, depthFromRoot),
                0),
            _ => new FutureRewardRouteEvaluation(path, [], [], [], [], [unknownPreview], 0d, 0)
        };

        if (resolved.EventRewards.Count == 0)
        {
            resolved = resolved with
            {
                EventRewards = [unknownPreview],
                RewardValue = resolved.RewardValue + unknownPreview.RewardValue * Math.Pow(FutureDepthDiscount, depthFromRoot)
            };
        }

        return resolved;
    }

    private FutureRewardRouteEvaluation EvaluateCombatRewardPoint(
        RunState runState,
        Player player,
        IReadOnlyList<MapPoint> path,
        RoomType roomType,
        RewardSimulationState state,
        int depthFromRoot,
        bool includeRelic)
    {
        List<FutureCardRewardPreview> cardRewards = [];
        List<FuturePotionRewardPreview> potionRewards = [];
        List<FutureRelicRewardPreview> relicRewards = [];
        List<FutureEventPreview> eventRewards = [];
        double rewardValue = 0d;
        double discount = Math.Pow(FutureDepthDiscount, depthFromRoot);

        if (TryPreviewEliteEncounterHazard(runState, player, roomType, depthFromRoot, out FutureEventPreview? encounterHazard) &&
            encounterHazard != null)
        {
            eventRewards.Add(encounterHazard);
            rewardValue += encounterHazard.RewardValue * discount;
        }

        if (TryGenerateCardReward(player, roomType, state, out FutureCardRewardPreview rewardPreview))
        {
            CardChoiceDecision decision = _cardChoiceEvaluator.EvaluateCandidates(
                rewardPreview.Cards,
                _cardChoiceEvaluator.ContextFactory.Create(
                    player,
                    CardChoiceSource.Reward,
                    skipAllowed: true,
                    debugSource: "future_reward_oracle"));

            CardEvaluationResult? best = decision.BestEvaluation;
            double cardRewardValue = 0d;
            if (best != null && decision.ShouldTakeCard)
            {
                cardRewardValue = Math.Max(0d, best.FinalScore - decision.SkipThreshold * 0.25d);
            }

            cardRewards.Add(rewardPreview with
            {
                BestCardId = best?.Candidate.CardId,
                BestCardScore = best?.FinalScore ?? 0d,
                RewardValue = cardRewardValue
            });
            rewardValue += cardRewardValue * discount;
        }

        FuturePotionRewardPreview potionPreview = RollPotionReward(player, roomType, state, depthFromRoot);
        if (potionPreview.WillDrop)
        {
            potionRewards.Add(potionPreview);
            rewardValue += EstimatePotionRewardRouteValue(player) * discount;
        }

        if (includeRelic &&
            TryGeneratePlayerRelicReward(player, state, depthFromRoot, "elite", out FutureRelicRewardPreview? relicPreview) &&
            relicPreview != null)
        {
            relicRewards.Add(relicPreview);
            rewardValue += relicPreview.RewardValue * discount;
        }

        return new FutureRewardRouteEvaluation(path, cardRewards, potionRewards, relicRewards, [], eventRewards, rewardValue, 1);
    }

    private static bool TryPreviewEliteEncounterHazard(
        RunState runState,
        Player player,
        RoomType roomType,
        int depthFromRoot,
        out FutureEventPreview? preview)
    {
        preview = null;
        if (roomType != RoomType.Elite ||
            !PhantasmalGardenersStrategy.TryGetNextEliteEncounterId(runState, out string encounterId) ||
            !PhantasmalGardenersStrategy.IsEncounterId(encounterId))
        {
            return false;
        }

        double penalty = PhantasmalGardenersStrategy.EstimateEliteRoutePenalty(player, depthFromRoot);
        preview = new FutureEventPreview(
            EventId: encounterId,
            RoomType: roomType,
            RewardValue: -penalty,
            Confidence: FutureOracleConfidence.Exact,
            DepthFromRoot: depthFromRoot,
            Reason: "predicted_next_elite_encounter");
        return true;
    }

    private FutureRewardRouteEvaluation EvaluateTreasurePoint(
        RunState runState,
        Player player,
        IReadOnlyList<MapPoint> path,
        RewardSimulationState state,
        int depthFromRoot)
    {
        List<FutureRelicRewardPreview> relicRewards = [];
        int relicChoices = Math.Max(1, runState.Players.Count);
        for (int index = 0; index < relicChoices; index++)
        {
            RelicRarity rarity = RollRelicRarity(state.TreasureRng);
            if (TryPullSharedRelicFromFront(player, state, rarity, shopOnly: false, out RelicModel? relic) && relic != null)
            {
                relicRewards.Add(BuildRelicPreview(player, relic, "treasure", depthFromRoot, FutureOracleConfidence.Exact));
            }
        }

        double bestRelicValue = relicRewards.Count > 0
            ? relicRewards.Max(static relic => relic.RewardValue)
            : EstimateRelicRarityValue(RelicRarity.Common);
        double rewardValue = bestRelicValue * Math.Pow(FutureDepthDiscount, depthFromRoot);
        return new FutureRewardRouteEvaluation(path, [], [], relicRewards, [], [], rewardValue, 0);
    }

    private FutureRewardRouteEvaluation EvaluateShopPoint(
        Player player,
        IReadOnlyList<MapPoint> path,
        RewardSimulationState state,
        int depthFromRoot)
    {
        if (!TryGenerateShopPreview(player, state, depthFromRoot, out FutureShopRewardPreview? shopPreview) ||
            shopPreview == null)
        {
            FutureShopRewardPreview fallback = new(
                CardIds: [],
                RelicIds: [],
                PotionIds: [],
                RewardValue: EstimateShopStrategicValue(player),
                Confidence: FutureOracleConfidence.Estimated,
                DepthFromRoot: depthFromRoot);
            return new FutureRewardRouteEvaluation(
                path,
                [],
                [],
                [],
                [fallback],
                [],
                fallback.RewardValue * Math.Pow(FutureDepthDiscount, depthFromRoot),
                0);
        }

        double rewardValue = shopPreview.RewardValue * Math.Pow(FutureDepthDiscount, depthFromRoot);
        return new FutureRewardRouteEvaluation(path, [], [], [], [shopPreview], [], rewardValue, 0);
    }

    private bool TryGenerateCardReward(
        Player player,
        RoomType roomType,
        RewardSimulationState state,
        out FutureCardRewardPreview preview)
    {
        preview = FutureCardRewardPreview.Empty;

        CardCreationOptions options = CardCreationOptions.ForRoom(player, roomType);
        List<CardModel> generatedCards = [];
        HashSet<string> blacklist = new(StringComparer.Ordinal);

        for (int i = 0; i < CardRewardOptionCount; i++)
        {
            if (!TryGenerateRewardCard(player, options, state, blacklist, out CardModel? card) || card == null)
            {
                continue;
            }

            generatedCards.Add(card);
            blacklist.Add(card.Id.Entry);
            state.RewardsRng.NextFloat();
        }

        if (generatedCards.Count == 0)
        {
            return false;
        }

        preview = new FutureCardRewardPreview(
            RoomType: roomType,
            CardIds: generatedCards.Select(static card => card.Id.Entry).ToArray(),
            Cards: generatedCards,
            BestCardId: null,
            BestCardScore: 0d,
            RewardValue: 0d);
        return true;
    }

    private static bool TryGenerateRewardCard(
        Player player,
        CardCreationOptions options,
        RewardSimulationState state,
        IReadOnlySet<string> blacklist,
        out CardModel? card)
    {
        card = null;
        List<CardModel> possibleCards = options.GetPossibleCards(player)
            .Where(card => !blacklist.Contains(card.Id.Entry))
            .Where(card => IsValidForPlayerCount(player, card))
            .ToList();
        if (possibleCards.Count == 0)
        {
            return false;
        }

        IEnumerable<CardModel> choices;
        if (options.RarityOdds == CardRarityOddsType.Uniform)
        {
            choices = possibleCards.Where(static card => card.Rarity is not CardRarity.Basic and not CardRarity.Ancient);
        }
        else
        {
            HashSet<CardRarity> allowedRarities = possibleCards
                .Select(static card => card.Rarity)
                .ToHashSet();
            CardRarity selectedRarity = RollRarity(state, options.RarityOdds);
            while (!allowedRarities.Contains(selectedRarity) && selectedRarity != CardRarity.None)
            {
                selectedRarity = selectedRarity.GetNextHighestRarity();
            }

            if (selectedRarity == CardRarity.None)
            {
                return false;
            }

            choices = possibleCards.Where(card => card.Rarity == selectedRarity);
        }

        card = state.RewardsRng.NextItem(choices.ToArray());
        return card != null;
    }

    private static CardRarity RollRarity(RewardSimulationState state, CardRarityOddsType oddsType)
    {
        float offset = oddsType == CardRarityOddsType.BossEncounter ? 0f : state.RarityOffset;
        float roll = state.RewardsRng.NextFloat();
        float rareThreshold = GetRareOdds(oddsType) + offset;
        CardRarity rarity;
        if (roll < rareThreshold)
        {
            rarity = CardRarity.Rare;
        }
        else if (roll < rareThreshold + GetUncommonOdds(oddsType))
        {
            rarity = CardRarity.Uncommon;
        }
        else
        {
            rarity = CardRarity.Common;
        }

        state.RarityOffset = rarity == CardRarity.Rare
            ? BaseRarityOffset
            : Math.Min(state.RarityOffset + state.RarityGrowth, MaxRarityOffset);
        return rarity;
    }

    private static float GetRareOdds(CardRarityOddsType oddsType)
    {
        return oddsType switch
        {
            CardRarityOddsType.EliteEncounter => CardRarityOdds.EliteRareOdds,
            CardRarityOddsType.BossEncounter => 1f,
            CardRarityOddsType.Shop => CardRarityOdds.ShopRareOdds,
            CardRarityOddsType.Uniform => 0.33f,
            _ => CardRarityOdds.RegularRareOdds
        };
    }

    private static float GetUncommonOdds(CardRarityOddsType oddsType)
    {
        return oddsType switch
        {
            CardRarityOddsType.EliteEncounter => 0.4f,
            CardRarityOddsType.BossEncounter => 0f,
            CardRarityOddsType.Shop => 0.37f,
            CardRarityOddsType.Uniform => 0.33f,
            _ => 0.37f
        };
    }

    private static bool IsValidForPlayerCount(Player player, CardModel card)
    {
        return player.RunState.Players.Count > 1
            ? card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly
            : card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly;
    }

    private RoomType RollUnknownRoomType(RunState runState, RewardSimulationState state)
    {
        if (state.UnknownRng == null)
        {
            return RoomType.Event;
        }

        UnknownMapPointOdds odds = new(state.UnknownRng)
        {
            MonsterOdds = state.UnknownMonsterOdds,
            EliteOdds = state.UnknownEliteOdds,
            TreasureOdds = state.UnknownTreasureOdds,
            ShopOdds = state.UnknownShopOdds
        };
        RoomType rolled = odds.Roll(Array.Empty<RoomType>(), runState);
        state.UnknownMonsterOdds = odds.MonsterOdds;
        state.UnknownEliteOdds = odds.EliteOdds;
        state.UnknownTreasureOdds = odds.TreasureOdds;
        state.UnknownShopOdds = odds.ShopOdds;
        return rolled;
    }

    private bool TryGenerateShopPreview(
        Player player,
        RewardSimulationState state,
        int depthFromRoot,
        out FutureShopRewardPreview? preview)
    {
        preview = null;
        List<CardModel> characterCards = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(static card => card.Rarity != CardRarity.Basic)
            .Where(card => IsValidForPlayerCount(player, card))
            .ToList();
        List<CardModel> colorlessCards = ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(static card => card.Rarity != CardRarity.Basic)
            .Where(card => IsValidForPlayerCount(player, card))
            .ToList();

        HashSet<string> usedCardIds = new(StringComparer.Ordinal);
        List<string> cardIds = [];
        List<string> relicIds = [];
        List<string> potionIds = [];
        List<(double Value, int Cost)> purchasable = [];

        CardType[] cardTypes = [CardType.Attack, CardType.Attack, CardType.Skill, CardType.Skill, CardType.Power];
        int saleIndex = state.ShopsRng.NextInt(cardTypes.Length);
        for (int index = 0; index < cardTypes.Length; index++)
        {
            if (!TryGenerateMerchantCard(player, characterCards, cardTypes[index], usedCardIds, state, out CardModel? card) || card == null)
            {
                continue;
            }

            int cost = EstimateMerchantCardCost(card, state.ShopsRng.NextFloat(0.95f, 1.05f), isOnSale: index == saleIndex);
            cardIds.Add(card.Id.Entry);
            purchasable.Add((EstimateCardPurchaseValue(player, card, cost), cost));
        }

        foreach (CardRarity rarity in new[] { CardRarity.Uncommon, CardRarity.Rare })
        {
            if (!TryGenerateMerchantColorlessCard(player, colorlessCards, rarity, usedCardIds, state, out CardModel? card) || card == null)
            {
                continue;
            }

            int cost = EstimateMerchantCardCost(card, state.ShopsRng.NextFloat(0.95f, 1.05f), isOnSale: false);
            cardIds.Add(card.Id.Entry);
            purchasable.Add((EstimateCardPurchaseValue(player, card, cost), cost));
        }

        RelicRarity[] relicRarities = [RollRelicRarity(state.RewardsRng), RollRelicRarity(state.RewardsRng), RelicRarity.Shop];
        foreach (RelicRarity rarity in relicRarities)
        {
            if (!TryPullPlayerRelicFromBack(player, state, rarity, shopOnly: true, out RelicModel? relic) || relic == null)
            {
                continue;
            }

            int cost = EstimateMerchantRelicCost(relic, state.ShopsRng.NextFloat(0.85f, 1.15f));
            relicIds.Add(relic.Id.Entry);
            purchasable.Add((EstimateRelicValue(player, relic) - cost / 7.5d, cost));
        }

        foreach ((PotionModel Potion, int Cost) potion in GenerateMerchantPotions(player, state, 3))
        {
            potionIds.Add(potion.Potion.Id.Entry);
            purchasable.Add((EstimatePotionPurchaseValue(player, potion.Potion) - potion.Cost / 10d, potion.Cost));
        }

        double shopValue = EstimateBestAffordableShopValue(player, purchasable);
        preview = new FutureShopRewardPreview(
            CardIds: cardIds,
            RelicIds: relicIds,
            PotionIds: potionIds,
            RewardValue: shopValue,
            Confidence: FutureOracleConfidence.Exact,
            DepthFromRoot: depthFromRoot);
        return cardIds.Count + relicIds.Count + potionIds.Count > 0;
    }

    private bool TryGenerateMerchantCard(
        Player player,
        IReadOnlyList<CardModel> cardPool,
        CardType type,
        HashSet<string> usedCardIds,
        RewardSimulationState state,
        out CardModel? card)
    {
        CardRarity rarity = RollShopCardRarity(state);
        List<CardModel> choices = cardPool
            .Where(card => card.Type == type && card.Rarity == rarity && !usedCardIds.Contains(card.Id.Entry))
            .ToList();
        while (choices.Count == 0 && rarity != CardRarity.None)
        {
            rarity = rarity.GetNextHighestRarity();
            choices = cardPool
                .Where(card => card.Type == type && card.Rarity == rarity && !usedCardIds.Contains(card.Id.Entry))
                .ToList();
        }

        card = choices.Count > 0 ? state.ShopsRng.NextItem(choices) : null;
        if (card != null)
        {
            usedCardIds.Add(card.Id.Entry);
            state.RewardsRng.NextFloat();
        }

        return card != null;
    }

    private bool TryGenerateMerchantColorlessCard(
        Player player,
        IReadOnlyList<CardModel> cardPool,
        CardRarity rarity,
        HashSet<string> usedCardIds,
        RewardSimulationState state,
        out CardModel? card)
    {
        List<CardModel> choices = cardPool
            .Where(card => card.Rarity == rarity && !usedCardIds.Contains(card.Id.Entry))
            .ToList();
        card = choices.Count > 0 ? state.ShopsRng.NextItem(choices) : null;
        if (card != null)
        {
            usedCardIds.Add(card.Id.Entry);
            state.RewardsRng.NextFloat();
        }

        return card != null;
    }

    private static CardRarity RollShopCardRarity(RewardSimulationState state)
    {
        float roll = state.RewardsRng.NextFloat();
        float rareThreshold = CardRarityOdds.ShopRareOdds + state.RarityOffset;
        if (roll < rareThreshold)
        {
            return CardRarity.Rare;
        }

        return roll < rareThreshold + 0.37f ? CardRarity.Uncommon : CardRarity.Common;
    }

    private double EstimateCardPurchaseValue(Player player, CardModel card, int cost)
    {
        CardChoiceDecision decision = _cardChoiceEvaluator.EvaluateCandidates(
            [card],
            _cardChoiceEvaluator.ContextFactory.Create(
                player,
                CardChoiceSource.Shop,
                skipAllowed: true,
                candidateGoldCost: cost,
                debugSource: "future_shop_oracle"));
        CardEvaluationResult? best = decision.BestEvaluation;
        if (best == null)
        {
            return 0d;
        }

        return Math.Max(0d, best.FinalScore - decision.SkipThreshold + 8d);
    }

    private static int EstimateMerchantCardCost(CardModel card, float roll, bool isOnSale)
    {
        int cost = card.Rarity switch
        {
            CardRarity.Rare => 150,
            CardRarity.Uncommon => 75,
            _ => 50
        };
        if (card.Pool is ColorlessCardPool)
        {
            cost = (int)Math.Round(cost * 1.15d);
        }

        cost = (int)Math.Round(cost * roll);
        return isOnSale ? cost / 2 : cost;
    }

    private static int EstimateMerchantRelicCost(RelicModel relic, float roll)
    {
        return (int)Math.Round(relic.MerchantCost * roll);
    }

    private static IEnumerable<(PotionModel Potion, int Cost)> GenerateMerchantPotions(
        Player player,
        RewardSimulationState state,
        int count)
    {
        List<PotionModel> options = PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>()).ToList();
        for (int i = 0; i < count; i++)
        {
            PotionRarity rarity = RollPotionRarity(state.ShopsRng);
            List<PotionModel> choices = options.Where(potion => potion.Rarity == rarity).ToList();
            PotionModel? potion = choices.Count > 0 ? state.ShopsRng.NextItem(choices) : null;
            if (potion == null)
            {
                continue;
            }

            options.Remove(potion);
            int baseCost = potion.Rarity switch
            {
                PotionRarity.Rare => 100,
                PotionRarity.Uncommon => 75,
                _ => 50
            };
            int cost = (int)Math.Round(baseCost * state.ShopsRng.NextFloat(0.95f, 1.05f));
            yield return (potion, cost);
        }
    }

    private static PotionRarity RollPotionRarity(Rng rng)
    {
        float roll = rng.NextFloat();
        if (roll <= 0.1f)
        {
            return PotionRarity.Rare;
        }

        return roll <= 0.35f ? PotionRarity.Uncommon : PotionRarity.Common;
    }

    private static double EstimatePotionPurchaseValue(Player player, PotionModel potion)
    {
        if (player.Relics.Any(static relic => relic.Id.Entry.Contains("SOZU", StringComparison.OrdinalIgnoreCase)))
        {
            return -20d;
        }

        double score = potion.Rarity switch
        {
            PotionRarity.Rare => 18d,
            PotionRarity.Uncommon => 13d,
            _ => 9d
        };
        if (!player.HasOpenPotionSlots)
        {
            score -= 6d;
        }

        return score;
    }

    private static double EstimateBestAffordableShopValue(Player player, IReadOnlyList<(double Value, int Cost)> purchasable)
    {
        int gold = Math.Max(player.Gold, 0);
        double total = 0d;
        foreach ((double value, int cost) in purchasable
                     .Where(static item => item.Value > 3d)
                     .OrderByDescending(static item => item.Value))
        {
            if (cost > gold)
            {
                continue;
            }

            gold -= cost;
            total += value;
        }

        total += EstimateRemovalServiceValue(player, gold);
        return Math.Min(85d, Math.Max(0d, total));
    }

    private static double EstimateRemovalServiceValue(Player player, int remainingGold)
    {
        if (remainingGold < 75)
        {
            return 0d;
        }

        int badCards = player.Deck.Cards.Count(card =>
            card.Rarity is CardRarity.Curse or CardRarity.Status ||
            card.Id.Entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase) ||
            card.Id.Entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase));
        return Math.Min(28d, badCards * 3.5d);
    }

    private bool TryGeneratePlayerRelicReward(
        Player player,
        RewardSimulationState state,
        int depthFromRoot,
        string source,
        out FutureRelicRewardPreview? preview)
    {
        RelicRarity rarity = RollRelicRarity(state.RewardsRng);
        if (!TryPullPlayerRelicFromFront(player, state, rarity, shopOnly: false, out RelicModel? relic) || relic == null)
        {
            preview = null;
            return false;
        }

        preview = BuildRelicPreview(player, relic, source, depthFromRoot, FutureOracleConfidence.Exact);
        return true;
    }

    private static FutureRelicRewardPreview BuildRelicPreview(
        Player player,
        RelicModel relic,
        string source,
        int depthFromRoot,
        FutureOracleConfidence confidence)
    {
        return new FutureRelicRewardPreview(
            Source: source,
            RelicId: relic.Id.Entry,
            Rarity: relic.Rarity.ToString(),
            RewardValue: EstimateRelicValue(player, relic),
            Confidence: confidence,
            DepthFromRoot: depthFromRoot);
    }

    private static RelicRarity RollRelicRarity(Rng rng)
    {
        float roll = rng.NextFloat();
        if (roll < 0.5f)
        {
            return RelicRarity.Common;
        }

        return roll < 0.83f ? RelicRarity.Uncommon : RelicRarity.Rare;
    }

    private static bool TryPullPlayerRelicFromFront(
        Player player,
        RewardSimulationState state,
        RelicRarity rarity,
        bool shopOnly,
        out RelicModel? relic)
    {
        return TryPullRelicFromBag(player, state.PlayerRelicBag, state.SharedRelicBag, rarity, fromBack: false, shopOnly, out relic);
    }

    private static bool TryPullPlayerRelicFromBack(
        Player player,
        RewardSimulationState state,
        RelicRarity rarity,
        bool shopOnly,
        out RelicModel? relic)
    {
        return TryPullRelicFromBag(player, state.PlayerRelicBag, state.SharedRelicBag, rarity, fromBack: true, shopOnly, out relic);
    }

    private static bool TryPullSharedRelicFromFront(
        Player player,
        RewardSimulationState state,
        RelicRarity rarity,
        bool shopOnly,
        out RelicModel? relic)
    {
        return TryPullRelicFromBag(player, state.SharedRelicBag, state.SharedRelicBag, rarity, fromBack: false, shopOnly, out relic);
    }

    private static bool TryPullRelicFromBag(
        Player player,
        Dictionary<RelicRarity, List<ModelId>> bag,
        Dictionary<RelicRarity, List<ModelId>> sharedBag,
        RelicRarity rarity,
        bool fromBack,
        bool shopOnly,
        out RelicModel? relic)
    {
        relic = null;
        foreach (RelicRarity candidateRarity in EnumerateRelicFallbackRarities(rarity))
        {
            if (!bag.TryGetValue(candidateRarity, out List<ModelId>? ids) || ids.Count == 0)
            {
                continue;
            }

            int start = fromBack ? ids.Count - 1 : 0;
            int endExclusive = fromBack ? -1 : ids.Count;
            int step = fromBack ? -1 : 1;
            for (int index = start; index != endExclusive; index += step)
            {
                RelicModel? model = ModelDb.GetByIdOrNull<RelicModel>(ids[index]);
                if (model == null ||
                    !model.IsAllowed(player.RunState) ||
                    shopOnly && !model.IsAllowedInShops)
                {
                    continue;
                }

                ids.RemoveAt(index);
                RemoveRelicFromBag(sharedBag, model.Id);
                relic = model;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<RelicRarity> EnumerateRelicFallbackRarities(RelicRarity rarity)
    {
        for (RelicRarity current = rarity; current != RelicRarity.None;)
        {
            yield return current;
            current = current switch
            {
                RelicRarity.Shop => RelicRarity.Common,
                RelicRarity.Common => RelicRarity.Uncommon,
                RelicRarity.Uncommon => RelicRarity.Rare,
                _ => RelicRarity.None
            };
        }
    }

    private static void RemoveRelicFromBag(Dictionary<RelicRarity, List<ModelId>> bag, ModelId relicId)
    {
        foreach (List<ModelId> ids in bag.Values)
        {
            ids.RemoveAll(id => string.Equals(id.Entry, relicId.Entry, StringComparison.Ordinal));
        }
    }

    private static double EstimateRelicValue(Player player, RelicModel relic)
    {
        double value = EstimateRelicRarityValue(relic.Rarity);
        string relicId = relic.Id.Entry.ToUpperInvariant();
        AddPattern("MEMBERSHIP", 18d);
        AddPattern("COURIER", 15d);
        AddPattern("BAG_OF_PREPARATION", 11d);
        AddPattern("ANCHOR", 9d);
        AddPattern("ORICHALCUM", 8d);
        AddPattern("PANTOGRAPH", 8d);
        AddPattern("VAJRA", 6d);
        if (player.Relics.Any(owned => string.Equals(owned.Id.Entry, relic.Id.Entry, StringComparison.Ordinal)))
        {
            value -= 24d;
        }

        return Math.Max(0d, value);

        void AddPattern(string token, double bonus)
        {
            if (relicId.Contains(token, StringComparison.Ordinal))
            {
                value += bonus;
            }
        }
    }

    private static double EstimateRelicRarityValue(RelicRarity rarity)
    {
        return rarity switch
        {
            RelicRarity.Rare => 40d,
            RelicRarity.Uncommon => 30d,
            RelicRarity.Shop => 34d,
            RelicRarity.Ancient => 48d,
            RelicRarity.Common => 22d,
            _ => 18d
        };
    }

    private static double EstimatePotionRewardRouteValue(Player player)
    {
        if (player.Relics.Any(static relic => relic.Id.Entry.Contains("SOZU", StringComparison.OrdinalIgnoreCase)))
        {
            return 0d;
        }

        return player.HasOpenPotionSlots ? 9d : 2d;
    }

    private static double EstimateShopStrategicValue(Player player)
    {
        double goldScale = Math.Min(player.Gold / 220d, 1.2d);
        double value = 16d + goldScale * 30d;
        int badOrBasicCards = player.Deck.Cards.Count(card =>
            card.Rarity is CardRarity.Curse or CardRarity.Status ||
            card.Id.Entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase) ||
            card.Id.Entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase));
        value += Math.Min(18d, badOrBasicCards * 2d);
        return value;
    }

    private static double EstimateUnknownRoomStrategicValue(Player player, RoomType resolvedRoom)
    {
        double hpRatio = player.Creature.MaxHp <= 0
            ? 1d
            : (double)player.Creature.CurrentHp / player.Creature.MaxHp;
        return resolvedRoom switch
        {
            RoomType.Event => hpRatio < 0.35d ? -6d : 12d,
            RoomType.Shop => EstimateShopStrategicValue(player) * 0.65d,
            RoomType.Treasure => 24d,
            RoomType.Elite => hpRatio >= 0.7d ? 26d : -18d,
            RoomType.Monster => hpRatio < 0.3d ? -8d : 9d,
            _ => 0d
        };
    }

    private static bool TryGetRewardRoomType(MapPointType pointType, out RoomType roomType)
    {
        roomType = pointType switch
        {
            MapPointType.Monster => RoomType.Monster,
            MapPointType.Elite => RoomType.Elite,
            MapPointType.Boss => RoomType.Boss,
            _ => default
        };
        return IsCombatRewardRoom(roomType);
    }

    private static FuturePotionRewardPreview RollPotionReward(
        Player player,
        RoomType roomType,
        RewardSimulationState state,
        int depthFromRoot)
    {
        float oddsBefore = state.PotionRewardOdds;
        PotionRewardOdds potionRewardOdds = new(oddsBefore, state.RewardsRng);
        bool willDrop = potionRewardOdds.Roll(player, RunManager.Instance.AscensionManager, roomType);
        state.PotionRewardOdds = potionRewardOdds.CurrentValue;

        return new FuturePotionRewardPreview(
            RoomType: roomType,
            WillDrop: willDrop,
            OddsBefore: oddsBefore,
            OddsAfter: state.PotionRewardOdds,
            DepthFromRoot: depthFromRoot);
    }

    private static bool IsCombatRewardRoom(RoomType roomType)
    {
        return roomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
    }

    private static IReadOnlyList<MapPoint> GetCurrentCandidates(RunState runState)
    {
        MapPoint? currentMapPoint = runState.CurrentMapPoint;
        if (currentMapPoint != null)
        {
            return currentMapPoint.Children
                .Where(candidate => runState.Map.HasPoint(candidate.coord))
                .ToArray();
        }

        if (!runState.CurrentMapCoord.HasValue)
        {
            return runState.Map.HasPoint(runState.Map.StartingMapPoint.coord)
                ? [runState.Map.StartingMapPoint]
                : [];
        }

        return [];
    }

    private static string FormatPoint(MapPoint point)
    {
        return $"{point.coord.col},{point.coord.row}";
    }

    private sealed class RewardSimulationState
    {
        private RewardSimulationState(
            Rng rewardsRng,
            Rng shopsRng,
            Rng treasureRng,
            Rng? unknownRng,
            float rarityOffset,
            float rarityGrowth,
            float potionRewardOdds,
            Dictionary<RelicRarity, List<ModelId>> playerRelicBag,
            Dictionary<RelicRarity, List<ModelId>> sharedRelicBag,
            float unknownMonsterOdds,
            float unknownEliteOdds,
            float unknownTreasureOdds,
            float unknownShopOdds)
        {
            RewardsRng = rewardsRng;
            ShopsRng = shopsRng;
            TreasureRng = treasureRng;
            UnknownRng = unknownRng;
            RarityOffset = rarityOffset;
            RarityGrowth = rarityGrowth;
            PotionRewardOdds = potionRewardOdds;
            PlayerRelicBag = playerRelicBag;
            SharedRelicBag = sharedRelicBag;
            UnknownMonsterOdds = unknownMonsterOdds;
            UnknownEliteOdds = unknownEliteOdds;
            UnknownTreasureOdds = unknownTreasureOdds;
            UnknownShopOdds = unknownShopOdds;
        }

        public Rng RewardsRng { get; }

        public Rng ShopsRng { get; }

        public Rng TreasureRng { get; }

        public Rng? UnknownRng { get; }

        public float RarityOffset { get; set; }

        public float RarityGrowth { get; }

        public float PotionRewardOdds { get; set; }

        public Dictionary<RelicRarity, List<ModelId>> PlayerRelicBag { get; }

        public Dictionary<RelicRarity, List<ModelId>> SharedRelicBag { get; }

        public float UnknownMonsterOdds { get; set; }

        public float UnknownEliteOdds { get; set; }

        public float UnknownTreasureOdds { get; set; }

        public float UnknownShopOdds { get; set; }

        public static RewardSimulationState From(Player player, RunState? runState)
        {
            Rng rewardsRng = new(player.PlayerRng.Rewards.Seed, player.PlayerRng.Rewards.Counter);
            Rng shopsRng = new(player.PlayerRng.Shops.Seed, player.PlayerRng.Shops.Counter);
            Rng treasureRng = runState != null
                ? new Rng(runState.Rng.TreasureRoomRelics.Seed, runState.Rng.TreasureRoomRelics.Counter)
                : new Rng(player.PlayerRng.Rewards.Seed, player.PlayerRng.Rewards.Counter);
            Rng? unknownRng = runState != null
                ? new Rng(runState.Rng.UnknownMapPoint.Seed, runState.Rng.UnknownMapPoint.Counter)
                : null;
            return new RewardSimulationState(
                rewardsRng,
                shopsRng,
                treasureRng,
                unknownRng,
                player.PlayerOdds.CardRarity.CurrentValue,
                player.PlayerOdds.CardRarity.RarityGrowth,
                player.PlayerOdds.PotionReward.CurrentValue,
                CopyRelicBag(player.RelicGrabBag.ToSerializable()),
                CopyRelicBag((runState?.SharedRelicGrabBag ?? player.RelicGrabBag).ToSerializable()),
                runState?.Odds.UnknownMapPoint.MonsterOdds ?? UnknownMapPointOdds.baseMonsterOdds,
                runState?.Odds.UnknownMapPoint.EliteOdds ?? UnknownMapPointOdds.baseEliteOdds,
                runState?.Odds.UnknownMapPoint.TreasureOdds ?? UnknownMapPointOdds.baseTreasureOdds,
                runState?.Odds.UnknownMapPoint.ShopOdds ?? UnknownMapPointOdds.baseShopOdds);
        }

        public RewardSimulationState Clone()
        {
            return new RewardSimulationState(
                new Rng(RewardsRng.Seed, RewardsRng.Counter),
                new Rng(ShopsRng.Seed, ShopsRng.Counter),
                new Rng(TreasureRng.Seed, TreasureRng.Counter),
                UnknownRng == null ? null : new Rng(UnknownRng.Seed, UnknownRng.Counter),
                RarityOffset,
                RarityGrowth,
                PotionRewardOdds,
                CopyRelicBag(PlayerRelicBag),
                CopyRelicBag(SharedRelicBag),
                UnknownMonsterOdds,
                UnknownEliteOdds,
                UnknownTreasureOdds,
                UnknownShopOdds);
        }

        private static Dictionary<RelicRarity, List<ModelId>> CopyRelicBag(SerializableRelicGrabBag bag)
        {
            return bag.RelicIdLists.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToList());
        }

        private static Dictionary<RelicRarity, List<ModelId>> CopyRelicBag(Dictionary<RelicRarity, List<ModelId>> bag)
        {
            return bag.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToList());
        }
    }
}

internal sealed record FutureRewardRouteEvaluation(
    IReadOnlyList<MapPoint> Path,
    IReadOnlyList<FutureCardRewardPreview> CardRewards,
    IReadOnlyList<FuturePotionRewardPreview> PotionRewards,
    IReadOnlyList<FutureRelicRewardPreview> RelicRewards,
    IReadOnlyList<FutureShopRewardPreview> ShopRewards,
    IReadOnlyList<FutureEventPreview> EventRewards,
    double RewardValue,
    int CombatRewardCount)
{
    public static readonly FutureRewardRouteEvaluation Empty = new([], [], [], [], [], [], 0d, 0);

    public string Describe()
    {
        string path = string.Join(">", Path.Select(static point => $"{point.coord.col},{point.coord.row}:{point.PointType}"));
        string rewards = string.Join(
            "; ",
            CardRewards
                .Where(static reward => reward.CardIds.Count > 0)
                .Select(static reward => $"{reward.RoomType}[{string.Join("/", reward.CardIds)}] best={reward.BestCardId ?? "none"}:{reward.BestCardScore:F1}"));
        string potions = string.Join("; ", PotionRewards.Select(static reward => reward.Describe()));
        string relics = string.Join("; ", RelicRewards.Select(static reward => reward.Describe()));
        string shops = string.Join("; ", ShopRewards.Select(static reward => reward.Describe()));
        string events = string.Join("; ", EventRewards.Select(static reward => reward.Describe()));
        return $"value={RewardValue:F1} combats={CombatRewardCount} path={path} rewards={rewards} potions={potions} relics={relics} shops={shops} events={events}";
    }

    public FutureRewardRouteEvaluation WithFuture(FutureRewardRouteEvaluation future, double discount)
    {
        return new FutureRewardRouteEvaluation(
            Path.Concat(future.Path).ToArray(),
            CardRewards.Concat(future.CardRewards).ToArray(),
            PotionRewards.Concat(future.PotionRewards).ToArray(),
            RelicRewards.Concat(future.RelicRewards).ToArray(),
            ShopRewards.Concat(future.ShopRewards).ToArray(),
            EventRewards.Concat(future.EventRewards).ToArray(),
            RewardValue + future.RewardValue * discount,
            CombatRewardCount + future.CombatRewardCount);
    }
}

internal sealed record FutureCardRewardPreview(
    RoomType RoomType,
    IReadOnlyList<string> CardIds,
    IReadOnlyList<CardModel> Cards,
    string? BestCardId,
    double BestCardScore,
    double RewardValue)
{
    public static readonly FutureCardRewardPreview Empty = new(default, [], [], null, 0d, 0d);
}

internal sealed record FuturePotionRewardPreview(
    RoomType RoomType,
    bool WillDrop,
    float OddsBefore,
    float OddsAfter,
    int DepthFromRoot)
{
    public static readonly FuturePotionRewardPreview Empty = new(default, false, 0f, 0f, 0);

    public string Describe()
    {
        return $"{RoomType}:drop={WillDrop} odds={OddsBefore:F2}->{OddsAfter:F2} depth={DepthFromRoot}";
    }
}

internal sealed record FutureRelicRewardPreview(
    string Source,
    string RelicId,
    string Rarity,
    double RewardValue,
    FutureOracleConfidence Confidence,
    int DepthFromRoot)
{
    public string Describe()
    {
        return $"{Source}:{RelicId}/{Rarity} value={RewardValue:F1} confidence={Confidence} depth={DepthFromRoot}";
    }
}

internal sealed record FutureShopRewardPreview(
    IReadOnlyList<string> CardIds,
    IReadOnlyList<string> RelicIds,
    IReadOnlyList<string> PotionIds,
    double RewardValue,
    FutureOracleConfidence Confidence,
    int DepthFromRoot)
{
    public string Describe()
    {
        return $"cards=[{string.Join("/", CardIds)}] relics=[{string.Join("/", RelicIds)}] potions=[{string.Join("/", PotionIds)}] value={RewardValue:F1} confidence={Confidence} depth={DepthFromRoot}";
    }
}

internal sealed record FutureEventPreview(
    string EventId,
    RoomType RoomType,
    double RewardValue,
    FutureOracleConfidence Confidence,
    int DepthFromRoot,
    string Reason)
{
    public string Describe()
    {
        return $"{EventId}:{RoomType} value={RewardValue:F1} confidence={Confidence} depth={DepthFromRoot} reason={Reason}";
    }
}

internal enum FutureOracleConfidence
{
    Exact,
    ExactRoomTypeEstimatedContents,
    Estimated
}
