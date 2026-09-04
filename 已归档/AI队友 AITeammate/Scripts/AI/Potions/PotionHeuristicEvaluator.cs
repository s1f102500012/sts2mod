using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal static class PotionHeuristicEvaluator
{
    private static readonly CardEvaluationContextFactory CardContextFactory = new();

    public static double EvaluateAcquisitionScore(
        Player player,
        DeckSummary deckSummary,
        string? potionId,
        string? rarity,
        int count,
        bool hasOpenPotionSlots,
        bool hasSozu,
        List<string>? reasons = null,
        bool applyShopAdjustments = false,
        int? shopCost = null,
        bool isAffordable = true,
        bool isLegalNow = true)
    {
        AiPotionAcquisitionWeights tuning = AiCharacterCombatConfigLoader.LoadForPlayer(player).Potions.Acquisition;
        double singlePotionScore = GetRarityBaseline(tuning, rarity);

        if (!hasOpenPotionSlots)
        {
            singlePotionScore -= tuning.NoOpenSlotPenalty;
            reasons?.Add($"no open potion slots -{tuning.NoOpenSlotPenalty:F1}");
        }

        if (hasSozu)
        {
            singlePotionScore -= tuning.SozuPenalty;
            reasons?.Add($"Sozu blocks procurement -{tuning.SozuPenalty:F1}");
        }

        if (!string.IsNullOrEmpty(potionId))
        {
            string upper = potionId.ToUpperInvariant();
            if (MatchesAny(upper, "BLOCK", "ARMOR", "DEX", "GHOST"))
            {
                double defenseBonus = deckSummary.BlockSources < 6
                    ? tuning.DefensiveCoverageLowNeedBonus
                    : tuning.DefensiveCoverageCoveredBonus;
                singlePotionScore += defenseBonus;
                reasons?.Add($"defensive coverage +{defenseBonus:F1}");
            }

            if (MatchesAny(upper, "FIRE", "EXPLOS", "ATTACK", "STRENGTH", "FEAR", "VULNERABLE"))
            {
                double offenseBonus = deckSummary.FrontloadDamageSources < 7
                    ? tuning.OffensiveCoverageLowNeedBonus
                    : tuning.OffensiveCoverageCoveredBonus;
                singlePotionScore += offenseBonus;
                reasons?.Add($"offensive reach +{offenseBonus:F1}");
            }

            if (MatchesAny(upper, "ENERGY", "DRAW", "GAMBLER", "AMBROSIA", "LIQUID"))
            {
                double tempoBonus = deckSummary.DrawSources < 2 || deckSummary.EnergySources < 1
                    ? tuning.TempoCoverageLowNeedBonus
                    : tuning.TempoCoverageCoveredBonus;
                singlePotionScore += tempoBonus;
                reasons?.Add($"tempo coverage +{tempoBonus:F1}");
            }

            if (MatchesAny(upper, "FAIRY", "HEART", "ELIXIR"))
            {
                singlePotionScore += tuning.HighLeverageEmergencyBonus;
                reasons?.Add($"high leverage emergency value +{tuning.HighLeverageEmergencyBonus:F1}");
            }
        }

        if (applyShopAdjustments)
        {
            if (shopCost.HasValue)
            {
                double costPenalty = shopCost.Value / tuning.ShopCostDivisor;
                singlePotionScore -= costPenalty;
                reasons?.Add($"costPenalty=-{costPenalty:F1}");
            }

            if (!isAffordable)
            {
                singlePotionScore -= tuning.UnaffordablePenalty;
                reasons?.Add($"currently unaffordable -{tuning.UnaffordablePenalty:F1}");
            }

            if (!isLegalNow)
            {
                singlePotionScore -= tuning.IllegalPenalty;
                reasons?.Add($"not immediately legal -{tuning.IllegalPenalty:F1}");
            }
        }

        double total = singlePotionScore * Math.Max(1, count);
        reasons?.Add($"potionTotal={total:F1}");
        return total;
    }

    public static bool TryChoosePotionToReplace(Player player, PotionModel incomingPotion, out PotionModel? potionToDiscard, out double incomingScore, out double discardScore)
    {
        potionToDiscard = null;
        discardScore = double.PositiveInfinity;
        DeckSummary deckSummary = CardContextFactory.Create(player, CardChoiceSource.Reward, skipAllowed: false, debugSource: "potion_replacement").DeckSummary;
        AiPotionRewardHandlingWeights handling = AiCharacterCombatConfigLoader.LoadForPlayer(player).Potions.RewardHandling;

        incomingScore = EvaluateHeldPotionScore(player, deckSummary, incomingPotion.Id.Entry, incomingPotion.Rarity.ToString());
        foreach (PotionModel currentPotion in player.Potions)
        {
            double currentScore = EvaluateHeldPotionScore(player, deckSummary, currentPotion.Id.Entry, currentPotion.Rarity.ToString());
            if (currentScore < discardScore)
            {
                discardScore = currentScore;
                potionToDiscard = currentPotion;
            }
        }

        return potionToDiscard != null && incomingScore >= discardScore + handling.ReplacementThreshold;
    }

    private static double EvaluateHeldPotionScore(Player player, DeckSummary deckSummary, string? potionId, string? rarity)
    {
        return EvaluateAcquisitionScore(
            player,
            deckSummary,
            potionId,
            rarity,
            count: 1,
            hasOpenPotionSlots: true,
            hasSozu: false);
    }

    private static double GetRarityBaseline(AiPotionAcquisitionWeights tuning, string? rarity)
    {
        return rarity switch
        {
            "Event" => tuning.EventBaseline,
            "Rare" => tuning.RareBaseline,
            "Uncommon" => tuning.UncommonBaseline,
            "Common" => tuning.CommonBaseline,
            _ => tuning.FallbackBaseline
        };
    }

    private static bool MatchesAny(string value, params string[] patterns)
    {
        foreach (string pattern in patterns)
        {
            if (value.Contains(pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
