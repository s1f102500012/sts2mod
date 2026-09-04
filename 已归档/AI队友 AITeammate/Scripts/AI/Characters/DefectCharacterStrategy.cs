using System;
using System.Linq;

namespace AITeammate.Scripts;

internal static class DefectCharacterStrategy
{
    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!IsDefectContext(context) || context.CurrentActIndex != 0)
        {
            return 0d;
        }

        bool earlyHalf = context.ActFloor <= 8 || context.TotalFloor <= 8;
        return CharacterRewardProfiles.Defect.Score(
            card,
            context,
            premiumAttackAdjust: (candidate, attackBonus) =>
            {
            if (context.DeckSummary.FrontloadDamageSources >= 7 &&
                NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                attackBonus *= 0.6d;
            }

            if (MatchesAny(candidate, "RAINBOW", "Rainbow", "彩虹") &&
                context.DeckSummary.OrbCards <= 2)
            {
                attackBonus *= 0.8d;
            }

                return attackBonus;
            },
            premiumDefenseAdjust: (candidate, defenseBonus) =>
            {
            if (NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                defenseBonus += 6d;
            }

            if (MatchesAny(candidate, "GENETIC_ALGORITHM", "GeneticAlgorithm", "遗传算法") &&
                earlyHalf)
            {
                defenseBonus += 6d;
            }

                return defenseBonus;
            },
            weakAttackAdjust: (candidate, attackPenalty) =>
            {
            if (MatchesAny(candidate, "CLAW", "Claw", "爪击") &&
                context.DeckCards.Count(deckCard => MatchesAny(deckCard, "CLAW", "Claw", "爪击")) >= 2)
            {
                attackPenalty *= 0.5d;
            }

                return attackPenalty;
            },
            weakDefenseAdjust: (_, defensePenalty) =>
            {
            if (context.DeckSummary.BlockSources <= 3)
            {
                defensePenalty *= 0.7d;
            }

                return defensePenalty;
            });
    }

    private static bool IsDefectContext(CardEvaluationContext context)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (characterId.Contains("DEFECT", StringComparison.Ordinal))
        {
            return true;
        }

        return context.DeckCards.Any(static card => MatchesAny(
            card,
            "STRIKE_DEFECT",
            "STRIKEDEFECT",
            "DEFEND_DEFECT",
            "DEFENDDEFECT",
            "ZAP",
            "DUALCAST",
            "DUAL_CAST"));
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
