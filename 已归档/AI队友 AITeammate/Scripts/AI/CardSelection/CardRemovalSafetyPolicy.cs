using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AITeammate.Scripts;

internal static class CardRemovalSafetyPolicy
{
    public const double MinimumWorthwhileRemovalBurden = 10d;

    public static bool IsWorthwhileRemovalBurden(double burden)
    {
        return burden >= MinimumWorthwhileRemovalBurden;
    }

    public static bool CanRemoveFromDeck(
        ResolvedCardView candidate,
        IReadOnlyList<ResolvedCardView> deckCards,
        out string reason)
    {
        reason = string.Empty;
        if (IsCurseLike(candidate) || IsStatusLike(candidate))
        {
            return true;
        }

        int starterStrikes = deckCards.Count(IsStarterStrike);
        int starterDefends = deckCards.Count(IsStarterDefend);
        if (IsStarterStrike(candidate))
        {
            if (starterStrikes <= 1)
            {
                reason = "keep at least one starter strike";
                return false;
            }

            return true;
        }

        if (IsStarterDefend(candidate))
        {
            if (starterDefends <= 1)
            {
                reason = "keep at least one starter defend";
                return false;
            }

            return true;
        }

        if (IsBasicNonStrikeDefend(candidate))
        {
            reason = "keep non-strike/defend starter card";
            return false;
        }

        return true;
    }

    public static double ApplyDeckRoleProtection(
        ResolvedCardView candidate,
        IReadOnlyList<ResolvedCardView> deckCards,
        Player? player,
        double burden,
        List<string> reasons)
    {
        if (deckCards.Count == 0)
        {
            return burden;
        }

        if (IsCurseLike(candidate))
        {
            burden += 180d;
            reasons.Add("curse-first removal priority +180.0");
            return burden;
        }

        if (IsStatusLike(candidate))
        {
            burden += 120d;
            reasons.Add("bad status removal priority +120.0");
            return burden;
        }

        bool noBadCardsLeft = deckCards.All(static card => !IsBadDeckCard(card));

        burden = ApplyStarterRotationPolicy(candidate, deckCards, player, burden, reasons, noBadCardsLeft);
        burden = ApplyHighValueKeepProtection(candidate, deckCards, burden, reasons);

        return burden;
    }

    private static double ApplyHighValueKeepProtection(
        ResolvedCardView candidate,
        IReadOnlyList<ResolvedCardView> deckCards,
        double burden,
        List<string> reasons)
    {
        if (candidate.Rarity == "Basic" ||
            candidate.Type is CardType.Status or CardType.Curse ||
            candidate.Ethereal)
        {
            return burden;
        }

        int knownValue = EstimateKnownPositiveValue(candidate);
        bool isEngineCard = candidate.GetCardsDrawn() > 0 && candidate.GetEnergyGain() > 0;
        bool isPremiumDefenseEngine = isEngineCard && candidate.GetEstimatedProtection() >= 8;
        bool isRelicGrantedKeepCard = IsCardIdLikeAny(candidate, "RELAX");
        if (knownValue < 24 &&
            !isEngineCard &&
            !isRelicGrantedKeepCard)
        {
            return burden;
        }

        double keepBias = 34d + Math.Max(0, knownValue - 20) * 1.4d;
        if (isEngineCard)
        {
            keepBias += 38d;
        }

        if (isPremiumDefenseEngine)
        {
            keepBias += 32d;
        }

        if (isRelicGrantedKeepCard)
        {
            keepBias += 90d;
        }

        int copies = deckCards.Count(card => string.Equals(card.CardId, candidate.CardId, StringComparison.Ordinal));
        if (copies > 1 && knownValue >= 24)
        {
            keepBias += Math.Min(copies - 1, 3) * 10d;
        }

        burden -= keepBias;
        reasons.Add($"high-value card keep bias -{keepBias:F1} value={knownValue} engine={isEngineCard}");
        return burden;
    }

    private static int EstimateKnownPositiveValue(ResolvedCardView card)
    {
        return card.GetEstimatedDamage() +
               card.GetEstimatedProtection() +
               (card.GetCardsDrawn() * 4) +
               (card.GetEnergyGain() * 5) +
               (card.GetEnemyVulnerableAmount() * 3) +
               (card.GetEnemyWeakAmount() * 3) +
               (card.GetEnemyPoisonAmount() * 3) +
               (card.GetSelfStrengthAmount() * 3) +
               (card.GetSelfDexterityAmount() * 3) +
               SpecialCardEffectHeuristics.EstimateCardSelectionUtility(card);
    }

    private static double ApplyStarterRotationPolicy(
        ResolvedCardView candidate,
        IReadOnlyList<ResolvedCardView> deckCards,
        Player? player,
        double burden,
        List<string> reasons,
        bool noBadCardsLeft)
    {
        if (!noBadCardsLeft)
        {
            return burden;
        }

        bool candidateIsStrike = IsStarterStrike(candidate);
        bool candidateIsDefend = IsStarterDefend(candidate);
        int starterStrikes = deckCards.Count(IsStarterStrike);
        int starterDefends = deckCards.Count(IsStarterDefend);
        if (!candidateIsStrike && !candidateIsDefend)
        {
            if (starterStrikes + starterDefends > 0 && candidate.Rarity != "Basic")
            {
                burden -= 18d;
                reasons.Add("keep non-basic while starter basics remain -18.0");
            }

            return burden;
        }

        (int baselineStrikes, int baselineDefends) = GetStarterBaseline(player, starterStrikes, starterDefends);
        int removedStrikes = Math.Max(0, baselineStrikes - starterStrikes);
        int removedDefends = Math.Max(0, baselineDefends - starterDefends);
        bool preferStrike = starterStrikes > starterDefends ||
                            starterStrikes == starterDefends &&
                            starterStrikes > 0 &&
                            removedStrikes <= removedDefends;
        bool preferDefend = starterDefends > starterStrikes ||
                            starterDefends == starterStrikes &&
                            starterDefends > 0 &&
                            !preferStrike;

        if (candidateIsStrike && preferStrike || candidateIsDefend && preferDefend)
        {
            int countGap = Math.Abs(starterStrikes - starterDefends);
            double rotationBonus = 52d +
                                   Math.Min(4, countGap) * 14d +
                                   Math.Min(3, Math.Abs(removedStrikes - removedDefends)) * 6d;
            burden += rotationBonus;
            reasons.Add($"starter strike/defend rotation priority +{rotationBonus:F1} strikes={starterStrikes} defends={starterDefends} removedS={removedStrikes} removedD={removedDefends}");
        }
        else
        {
            double rotationKeepBias = 58d + Math.Min(3, Math.Abs(starterStrikes - starterDefends)) * 10d;
            burden -= rotationKeepBias;
            reasons.Add($"starter strike/defend rotation keep bias -{rotationKeepBias:F1} strikes={starterStrikes} defends={starterDefends}");
        }

        return burden;
    }

    private static (int Strikes, int Defends) GetStarterBaseline(
        Player? player,
        int currentStarterStrikes,
        int currentStarterDefends)
    {
        string characterId = player?.Character?.Id.Entry ?? string.Empty;
        int baselineStrikes = characterId.Contains("DEFECT", StringComparison.OrdinalIgnoreCase) ? 4 : 5;
        const int baselineDefends = 4;
        return (Math.Max(baselineStrikes, currentStarterStrikes), Math.Max(baselineDefends, currentStarterDefends));
    }

    private static bool IsBadDeckCard(ResolvedCardView card)
    {
        return IsCurseLike(card) || IsStatusLike(card);
    }

    private static bool IsCurseLike(ResolvedCardView card)
    {
        return card.Type == CardType.Curse || card.Rarity == "Curse";
    }

    private static bool IsStatusLike(ResolvedCardView card)
    {
        return card.Type == CardType.Status || card.Rarity == "Status";
    }

    private static bool IsBasicNonStrikeDefend(ResolvedCardView card)
    {
        return card.Rarity == "Basic" &&
               !IsStarterStrike(card) &&
               !IsStarterDefend(card);
    }

    private static bool IsStarterStrike(ResolvedCardView card)
    {
        return card.Rarity == "Basic" && IsCardIdLike(card, "STRIKE");
    }

    private static bool IsStarterDefend(ResolvedCardView card)
    {
        return card.Rarity == "Basic" && IsCardIdLike(card, "DEFEND");
    }

    private static bool IsCardIdLike(ResolvedCardView card, string token)
    {
        return card.CardId.Contains(token, StringComparison.OrdinalIgnoreCase) ||
               card.Name.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCardIdLikeAny(ResolvedCardView card, params string[] tokens)
    {
        return tokens.Any(token => IsCardIdLike(card, token));
    }
}
