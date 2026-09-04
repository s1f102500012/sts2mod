using System;
using System.Linq;

namespace AITeammate.Scripts;

internal static class SilentCharacterStrategy
{
    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!IsSilentContext(context) || context.CurrentActIndex != 0)
        {
            return 0d;
        }

        return CharacterRewardProfiles.Silent.Score(
            card,
            context,
            premiumAttackAdjust: (candidate, attackBonus) =>
            {
            if (context.DeckSummary.FrontloadDamageSources >= 7 &&
                NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                attackBonus *= 0.6d;
            }

            if (MatchesAny(candidate, "ACCURACY", "精密瞄准") &&
                context.DeckSummary.ZeroCostCards <= 1)
            {
                attackBonus *= 0.7d;
            }

                return attackBonus;
            },
            weakAttackAdjust: (candidate, attackPenalty) =>
            {
            if (MatchesAny(candidate, "BLADE_DANCE", "BladeDance", "刀刃之舞") &&
                context.DeckSummary.ZeroCostCards >= 3)
            {
                attackPenalty *= 0.55d;
            }

            if (MatchesAny(candidate, "FINISHER", "Finisher", "终结技") &&
                context.DeckSummary.AttackCount >= 7)
            {
                attackPenalty *= 0.65d;
            }

                return attackPenalty;
            },
            premiumDefenseAdjust: (candidate, defenseBonus) =>
            {
            if (NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                defenseBonus += 6d;
            }

            if (MatchesAny(candidate, "WRAITH_FORM", "触不可及") &&
                context.ActFloor <= 6)
            {
                defenseBonus *= 0.75d;
            }

                return defenseBonus;
            },
            weakDefenseAdjust: (candidate, defensePenalty) =>
            {
            if (MatchesAny(candidate, "AFTER_IMAGE", "AfterImage", "残影") &&
                context.DeckSummary.ZeroCostCards >= 4)
            {
                defensePenalty *= 0.5d;
            }

            if (MatchesAny(candidate, "MALAISE", "Malaise", "萎靡") &&
                context.ActFloor >= 10)
            {
                defensePenalty *= 0.6d;
            }

                return defensePenalty;
            });
    }

    private static bool IsSilentContext(CardEvaluationContext context)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (characterId.Contains("SILENT", StringComparison.Ordinal))
        {
            return true;
        }

        return context.DeckCards.Any(static card => MatchesAny(
            card,
            "STRIKE_SILENT",
            "STRIKESILENT",
            "DEFEND_SILENT",
            "DEFENDSILENT",
            "NEUTRALIZE",
            "SURVIVOR"));
    }

    private static bool NeedsDefenseMoreThanDamage(DeckSummary deck)
    {
        return deck.BlockSources < 5 || deck.QualityDefenseSources < 3;
    }

    private static bool MatchesAny(ResolvedCardView card, params string[] tokens)
    {
        return ActOneRewardProfile.MatchesAny(card, tokens);
    }

    private static string Normalize(string value)
    {
        return ActOneRewardProfile.Normalize(value);
    }
}
