using System;
using System.Linq;

namespace AITeammate.Scripts;

internal static class NecrobinderCharacterStrategy
{
    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!IsNecrobinderContext(context) || context.CurrentActIndex != 0)
        {
            return 0d;
        }

        return CharacterRewardProfiles.Necrobinder.Score(
            card,
            context,
            premiumAttackAdjust: (_, attackBonus) =>
            {
            if (context.DeckSummary.FrontloadDamageSources >= 7 &&
                NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                attackBonus *= 0.6d;
            }

                return attackBonus;
            },
            premiumDefenseAdjust: (_, defenseBonus) =>
            {
            if (NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                defenseBonus += 6d;
            }

                return defenseBonus;
            },
            weakAttackAdjust: (_, attackPenalty) =>
            {
            if (context.DeckSummary.FrontloadDamageSources <= 3)
            {
                attackPenalty *= 0.75d;
            }

                return attackPenalty;
            },
            weakDefenseAdjust: (_, defensePenalty) =>
            {
            if (NeedsDefenseMoreThanDamage(context.DeckSummary) && context.DeckSummary.BlockSources <= 3)
            {
                defensePenalty *= 0.75d;
            }

                return defensePenalty;
            });
    }

    private static bool IsNecrobinderContext(CardEvaluationContext context)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (characterId.Contains("NECROBINDER", StringComparison.Ordinal) ||
            characterId.Contains("NECRO", StringComparison.Ordinal))
        {
            return true;
        }

        return context.DeckCards.Any(static card => MatchesAny(
            card,
            "STRIKE_NECROBINDER",
            "STRIKENECROBINDER",
            "DEFEND_NECROBINDER",
            "DEFENDNECROBINDER",
            "POKE",
            "DEFY",
            "NEGATIVE_PULSE"));
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
