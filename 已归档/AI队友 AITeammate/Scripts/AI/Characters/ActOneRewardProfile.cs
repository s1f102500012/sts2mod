using System;
using System.Linq;

namespace AITeammate.Scripts;

internal sealed class ActOneRewardProfile
{
    private const double StrongBonus = 22d;
    private const double SoftBonus = 15d;
    private const double StrongPenalty = 24d;
    private const double SoftPenalty = 16d;

    public required string[] PremiumAttackTokens { get; init; }

    public required string[] WeakAttackTokens { get; init; }

    public required string[] PremiumDefenseTokens { get; init; }

    public required string[] WeakDefenseTokens { get; init; }

    public double Score(
        ResolvedCardView card,
        CardEvaluationContext context,
        Func<ResolvedCardView, double, double>? premiumAttackAdjust = null,
        Func<ResolvedCardView, double, double>? weakAttackAdjust = null,
        Func<ResolvedCardView, double, double>? premiumDefenseAdjust = null,
        Func<ResolvedCardView, double, double>? weakDefenseAdjust = null)
    {
        if (context.CurrentActIndex != 0)
        {
            return 0d;
        }

        bool earlyHalf = context.ActFloor <= 8 || context.TotalFloor <= 8;
        double bonus = earlyHalf ? StrongBonus : SoftBonus;
        double penalty = earlyHalf ? StrongPenalty : SoftPenalty;
        double score = 0d;

        if (MatchesAny(card, PremiumAttackTokens))
        {
            score += premiumAttackAdjust?.Invoke(card, bonus) ?? bonus;
        }

        if (MatchesAny(card, WeakAttackTokens))
        {
            score -= weakAttackAdjust?.Invoke(card, penalty) ?? penalty;
        }

        if (MatchesAny(card, PremiumDefenseTokens))
        {
            score += premiumDefenseAdjust?.Invoke(card, bonus) ?? bonus;
        }

        if (MatchesAny(card, WeakDefenseTokens))
        {
            score -= weakDefenseAdjust?.Invoke(card, penalty) ?? penalty;
        }

        if (score > 0d && context.ChoiceSource == CardChoiceSource.Shop)
        {
            score *= 0.9d;
        }

        return score;
    }

    public static bool MatchesAny(ResolvedCardView card, params string[] tokens)
    {
        string normalizedId = Normalize(card.CardId);
        string normalizedName = Normalize(card.Name);
        return tokens.Any(token =>
        {
            string normalizedToken = Normalize(token);
            return normalizedId.Contains(normalizedToken, StringComparison.Ordinal) ||
                   normalizedName.Contains(normalizedToken, StringComparison.Ordinal);
        });
    }

    public static string Normalize(string value)
    {
        return value.Replace(' ', '_').Replace('-', '_').Replace(':', '_').Replace('/', '_').ToUpperInvariant();
    }
}
