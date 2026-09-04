using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal static class LagavulinMatriarchStrategy
{
    private static readonly string[] PersistentDownsideExactTokens =
    [
        "BLOODLETTING",
        "OFFERING",
        "HEMOKINESIS",
        "SACRIFICE",
        "HP_LOSS",
        "HPLOSS",
        "SELF_DAMAGE",
        "SELFHARM",
        "WOUND",
        "DAZED",
        "BURN",
        "VOID",
        "SLIMED",
        "CURSE"
    ];

    private static readonly string[] PersistentDownsidePhraseTokens =
    [
        "LOSE_HP",
        "LOSE_HEALTH",
        "LOSE_LIFE",
        "ADD_STATUS",
        "ADDSTATUS",
        "SHUFFLE_STATUS",
        "SHUFFLESTATUS",
        "STATUS_INTO",
        "STATUS_TO",
        "INTO_DRAW_PILE",
        "INTO_DISCARD_PILE"
    ];

    private static readonly string[] PersistentSelfDebuffTokens =
    [
        "FRAIL",
        "VULNERABLE",
        "WEAK",
        "LOSE_STRENGTH",
        "LOSE_DEXTERITY"
    ];

    public static bool IsForbiddenOpeningAction(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView? card)
    {
        if (!context.IsLagavulinMatriarchOpeningSetupWindow ||
            string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(action.ActionType, AiTeammateActionKind.PlayCard.ToString(), StringComparison.Ordinal))
        {
            return true;
        }

        return !IsAllowedOpeningCard(card);
    }

    public static bool IsAllowedOpeningCard(ResolvedCardView? card)
    {
        if (card == null)
        {
            return false;
        }

        return card.Type switch
        {
            CardType.Power => true,
            CardType.Attack => HasEnemyDebuff(card),
            CardType.Skill => !HasPersistentDownside(card),
            _ => false
        };
    }

    public static bool HasEnemyDebuff(ResolvedCardView? card)
    {
        return card.GetEnemyVulnerableAmount() > 0 ||
               card.GetEnemyWeakAmount() > 0 ||
               card.GetEnemyPoisonAmount() > 0 ||
               SpecialCardEffectHeuristics.GetSpecialEnemyVulnerableAmount(card) > 0 ||
               SpecialCardEffectHeuristics.GetSpecialEnemyWeakAmount(card) > 0;
    }

    private static bool HasPersistentDownside(ResolvedCardView card)
    {
        if (AppliesPersistentSelfDebuff(card))
        {
            return true;
        }

        string normalized = card.GetNormalizedSearchToken();
        string metadata = ResolvedCardViewExtensions.NormalizeSearchToken(
            string.Join(' ', card.Keywords.Concat(card.Tags)));
        string joined = $"{normalized}_{metadata}";
        string delimited = $"_{joined}_";

        return PersistentDownsideExactTokens.Any(token =>
                   delimited.Contains($"_{ResolvedCardViewExtensions.NormalizeSearchToken(token)}_", StringComparison.Ordinal)) ||
               PersistentDownsidePhraseTokens.Any(token =>
                   joined.Contains(ResolvedCardViewExtensions.NormalizeSearchToken(token), StringComparison.Ordinal));
    }

    private static bool AppliesPersistentSelfDebuff(ResolvedCardView card)
    {
        return card.Effects.Any(effect =>
            effect.Kind == EffectKind.ApplyPower &&
            effect.TargetScope == TargetScope.Self &&
            effect.DurationHint != DurationHint.ThisTurn &&
            !string.IsNullOrEmpty(effect.AppliedPowerId) &&
            PersistentSelfDebuffTokens.Any(token =>
                ResolvedCardViewExtensions.NormalizeSearchToken(effect.AppliedPowerId)
                    .Contains(ResolvedCardViewExtensions.NormalizeSearchToken(token), StringComparison.Ordinal)));
    }
}
