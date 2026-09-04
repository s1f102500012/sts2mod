using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal static class StatusCardStrategy
{
    private static readonly string[] NegativeStatusTokens =
    [
        "ASCENDER",
        "BECKON",
        "BURN",
        "CLUMSY",
        "CURSE",
        "DAZED",
        "DECAY",
        "DOUBT",
        "INJURY",
        "MELANCHOLY",
        "NORMALITY",
        "PAIN",
        "PARASITE",
        "REGRET",
        "SHAME",
        "SLIMED",
        "STATUS",
        "VOID",
        "WOUND"
    ];

    private static readonly string[] ControlledHandCleanupTokens =
    [
        "ACROBATICS",
        "BURNING_PACT",
        "CALCULATED_GAMBLE",
        "CLEANSE",
        "DISCARD",
        "EXHAUST",
        "FIEND_FIRE",
        "GAMBLE",
        "PREPARED",
        "PURGE",
        "RECYCLE",
        "SECOND_WIND",
        "SEVER_SOUL",
        "SURVIVOR",
        "TRUE_GRIT"
    ];

    private static readonly string[] UnsafeWholeHandCleanupTokens =
    [
        "CALCULATED_GAMBLE",
        "FIEND_FIRE",
        "SECOND_WIND",
        "SEVER_SOUL"
    ];

    public static bool IsNegativeStatusOrCurse(ResolvedCardView? card)
    {
        if (card == null)
        {
            return false;
        }

        if (card.Rarity is "Status" or "Curse")
        {
            return true;
        }

        string token = Normalize(card.CardId + "_" + card.Name);
        return NegativeStatusTokens.Any(token.Contains);
    }

    public static bool IsBeckon(ResolvedCardView? card)
    {
        return HasAnyToken(card, "BECKON");
    }

    public static int CountNegativeStatusOrCurse(IEnumerable<ResolvedCardView> cards)
    {
        return cards.Count(IsNegativeStatusOrCurse);
    }

    public static double GetBurdenScore(ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0d;
        }

        return GetStatusBurden(card.Rarity, Normalize(card.CardId + "_" + card.Name));
    }

    public static bool IsLikelyHandCleanupCard(ResolvedCardView? card)
    {
        if (card == null || IsNegativeStatusOrCurse(card))
        {
            return false;
        }

        string token = Normalize(card.CardId + "_" + card.Name);
        return ControlledHandCleanupTokens.Any(token.Contains);
    }

    public static bool IsControlledHandCleanupCard(ResolvedCardView? card)
    {
        if (!IsLikelyHandCleanupCard(card))
        {
            return false;
        }

        return !IsUnsafeWholeHandCleanupCard(card);
    }

    public static bool IsUnsafeWholeHandCleanupCard(ResolvedCardView? card)
    {
        if (card == null)
        {
            return false;
        }

        string token = Normalize(card.CardId + "_" + card.Name);
        return UnsafeWholeHandCleanupTokens.Any(token.Contains);
    }

    public static bool IsAllowedHandCleanupTarget(ResolvedCardView? card)
    {
        return IsAllowedHandCleanupTarget(card, null);
    }

    public static bool IsAllowedHandCleanupTarget(ResolvedCardView? card, Player? player)
    {
        if (card == null)
        {
            return false;
        }

        if (IsNegativeStatusOrCurse(card) || IsTacticianLikeCard(card))
        {
            return true;
        }

        return CanSpendStarterStrikeOrDefend(card, player);
    }

    public static bool IsAllowedHandCleanupTarget(CardModel? card)
    {
        return IsAllowedHandCleanupTarget(card, null);
    }

    public static bool IsAllowedHandCleanupTarget(CardModel? card, Player? player)
    {
        if (card == null)
        {
            return false;
        }

        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        string rarity = card.Rarity.ToString();
        if (rarity is "Status" or "Curse" ||
            NegativeStatusTokens.Any(token.Contains) ||
            IsTacticianLikeCard(card))
        {
            return true;
        }

        return CanSpendStarterStrikeOrDefend(card, player);
    }

    public static int CountAllowedHandCleanupTargets(IEnumerable<ResolvedCardView> cards)
    {
        return CountAllowedHandCleanupTargets(cards, null);
    }

    public static int CountAllowedHandCleanupTargets(IEnumerable<ResolvedCardView> cards, Player? player)
    {
        return cards.Count(card => IsAllowedHandCleanupTarget(card, player));
    }

    public static double SumAllowedCleanupBurden(IEnumerable<ResolvedCardView> cards)
    {
        return SumAllowedCleanupBurden(cards, null);
    }

    public static double SumAllowedCleanupBurden(IEnumerable<ResolvedCardView> cards, Player? player)
    {
        return cards.Where(card => IsAllowedHandCleanupTarget(card, player)).Sum(GetBurdenScore);
    }

    public static IReadOnlyList<CardModel> RankHandCleanupTargets(IEnumerable<CardModel> cards, Player? player)
    {
        List<CardModel> candidates = cards
            .Where(card => IsAllowedHandCleanupTarget(card, player))
            .ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        List<CardModel> statusCards = candidates
            .Where(IsNegativeStatusOrCurse)
            .OrderByDescending(GetBurdenScore)
            .ThenBy(static card => card.Title)
            .ToList();
        if (statusCards.Count > 0)
        {
            return statusCards;
        }

        List<CardModel> tacticianCards = candidates
            .Where(IsTacticianLikeCard)
            .OrderBy(static card => card.Title)
            .ToList();
        if (tacticianCards.Count > 0)
        {
            return tacticianCards;
        }

        return candidates
            .Where(IsStarterStrikeOrDefend)
            .OrderByDescending(card => ScoreStarterCleanupTarget(card, player))
            .ThenBy(static card => card.Title)
            .ToList();
    }

    public static bool IsNegativeStatusOrCurse(CardModel? card)
    {
        if (card == null)
        {
            return false;
        }

        string rarity = card.Rarity.ToString();
        if (rarity is "Status" or "Curse")
        {
            return true;
        }

        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return NegativeStatusTokens.Any(token.Contains);
    }

    public static double GetBurdenScore(CardModel? card)
    {
        if (card == null)
        {
            return 0d;
        }

        return GetStatusBurden(card.Rarity.ToString(), Normalize($"{card.Id.Entry}_{card.Title}"));
    }

    public static int EstimateKnownBadDraws(DeterministicCombatContext context, int cardsDrawn)
    {
        if (cardsDrawn <= 0)
        {
            return 0;
        }

        return context.KnownDrawPileTopCards
            .Take(cardsDrawn)
            .Count(IsNegativeStatusOrCurse);
    }

    public static double SumBurdenScore(IEnumerable<ResolvedCardView> cards)
    {
        return cards.Sum(GetBurdenScore);
    }

    public static bool HasAnyToken(ResolvedCardView? card, params string[] tokens)
    {
        if (card == null)
        {
            return false;
        }

        string cardToken = Normalize(card.CardId + "_" + card.Name);
        return tokens.Any(token => cardToken.Contains(Normalize(token), StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        return value.Replace(' ', '_')
            .Replace('-', '_')
            .Replace(':', '_')
            .Replace('/', '_')
            .ToUpperInvariant();
    }

    private static double GetStatusBurden(string rarity, string token)
    {
        double burden = rarity switch
        {
            "Curse" => 64d,
            "Status" => 46d,
            _ => 0d
        };

        AddTokenBurden("BECKON", 62d);
        AddTokenBurden("VOID", 34d);
        AddTokenBurden("NORMALITY", 32d);
        AddTokenBurden("REGRET", 30d);
        AddTokenBurden("PAIN", 30d);
        AddTokenBurden("BURN", 28d);
        AddTokenBurden("DECAY", 28d);
        AddTokenBurden("WOUND", 24d);
        AddTokenBurden("DAZED", 18d);
        AddTokenBurden("MELANCHOLY", 18d);
        AddTokenBurden("SLIMED", 14d);

        if (burden <= 0d && NegativeStatusTokens.Any(token.Contains))
        {
            burden = 36d;
        }

        return burden;

        void AddTokenBurden(string statusToken, double value)
        {
            if (token.Contains(statusToken, StringComparison.Ordinal))
            {
                burden = Math.Max(burden, value);
            }
        }
    }

    private static double ScoreStarterCleanupTarget(CardModel card, Player? player)
    {
        bool isStrike = IsStarterStrike(card);
        bool isDefend = IsStarterDefend(card);
        if (!isStrike && !isDefend)
        {
            return double.NegativeInfinity;
        }

        if (player == null)
        {
            return isDefend ? 8d : 6d;
        }

        IReadOnlyList<CardModel> deck = player.Deck.Cards;
        int starterStrikes = deck.Count(IsStarterStrike);
        int starterDefends = deck.Count(IsStarterDefend);
        int damageSources = deck.Count(IsReliableDamageSource);
        int blockSources = deck.Count(IsReliableBlockSource);

        bool damagePoor = damageSources <= Math.Max(4, blockSources - 1);
        bool defensePoor = blockSources <= Math.Max(3, damageSources - 2);
        double score = 0d;
        if (isStrike)
        {
            score += defensePoor ? 35d : damagePoor ? -90d : 8d;
            if (starterStrikes <= 2)
            {
                score -= 80d;
            }
        }

        if (isDefend)
        {
            score += damagePoor ? 35d : defensePoor ? -90d : 9d;
            if (starterDefends <= 2)
            {
                score -= 70d;
            }
        }

        return score;
    }

    private static bool CanSpendStarterStrikeOrDefend(ResolvedCardView card, Player? player)
    {
        if (!IsStarterStrikeOrDefend(card))
        {
            return false;
        }

        if (player == null)
        {
            return true;
        }

        IReadOnlyList<CardModel> deck = player.Deck.Cards;
        if (IsStarterStrike(card))
        {
            return deck.Count(IsStarterStrike) > 1;
        }

        if (IsStarterDefend(card))
        {
            return deck.Count(IsStarterDefend) > 1;
        }

        return false;
    }

    private static bool CanSpendStarterStrikeOrDefend(CardModel card, Player? player)
    {
        if (!IsStarterStrikeOrDefend(card))
        {
            return false;
        }

        if (player == null)
        {
            return true;
        }

        IReadOnlyList<CardModel> deck = player.Deck.Cards;
        if (IsStarterStrike(card))
        {
            return deck.Count(IsStarterStrike) > 1;
        }

        if (IsStarterDefend(card))
        {
            return deck.Count(IsStarterDefend) > 1;
        }

        return false;
    }

    private static bool IsReliableDamageSource(CardModel card)
    {
        if (IsNegativeStatusOrCurse(card))
        {
            return false;
        }

        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return card.Type == CardType.Attack ||
               token.Contains("STRIKE", StringComparison.Ordinal) ||
               token.Contains("DAMAGE", StringComparison.Ordinal);
    }

    private static bool IsReliableBlockSource(CardModel card)
    {
        if (IsNegativeStatusOrCurse(card))
        {
            return false;
        }

        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return card.GainsBlock ||
               token.Contains("DEFEND", StringComparison.Ordinal) ||
               token.Contains("BLOCK", StringComparison.Ordinal);
    }

    private static bool IsStarterStrikeOrDefend(ResolvedCardView card)
    {
        string token = Normalize(card.CardId + "_" + card.Name);
        return card.Rarity == "Basic" &&
               (token.Contains("STRIKE", StringComparison.Ordinal) ||
                token.Contains("DEFEND", StringComparison.Ordinal));
    }

    private static bool IsStarterStrikeOrDefend(CardModel card)
    {
        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return card.Rarity.ToString() == "Basic" &&
               (token.Contains("STRIKE", StringComparison.Ordinal) ||
                token.Contains("DEFEND", StringComparison.Ordinal));
    }

    private static bool IsStarterStrike(CardModel card)
    {
        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return card.Rarity.ToString() == "Basic" &&
               token.Contains("STRIKE", StringComparison.Ordinal);
    }

    private static bool IsStarterStrike(ResolvedCardView card)
    {
        string token = Normalize(card.CardId + "_" + card.Name);
        return card.Rarity == "Basic" &&
               token.Contains("STRIKE", StringComparison.Ordinal);
    }

    private static bool IsStarterDefend(CardModel card)
    {
        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return card.Rarity.ToString() == "Basic" &&
               token.Contains("DEFEND", StringComparison.Ordinal);
    }

    private static bool IsStarterDefend(ResolvedCardView card)
    {
        string token = Normalize(card.CardId + "_" + card.Name);
        return card.Rarity == "Basic" &&
               token.Contains("DEFEND", StringComparison.Ordinal);
    }

    private static bool IsTacticianLikeCard(ResolvedCardView card)
    {
        string token = Normalize(card.CardId + "_" + card.Name);
        return token.Contains("TACTICIAN", StringComparison.Ordinal) ||
               token.Contains("TACTIC", StringComparison.Ordinal);
    }

    private static bool IsTacticianLikeCard(CardModel card)
    {
        string token = Normalize($"{card.Id.Entry}_{card.Title}");
        return token.Contains("TACTICIAN", StringComparison.Ordinal) ||
               token.Contains("TACTIC", StringComparison.Ordinal);
    }
}
