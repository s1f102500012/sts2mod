using System;
using System.Linq;

namespace AITeammate.Scripts;

internal static class IroncladCharacterStrategy
{
    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!IsIroncladContext(context) || context.CurrentActIndex != 0)
        {
            return 0d;
        }

        return CharacterRewardProfiles.Ironclad.Score(
            card,
            context,
            premiumAttackAdjust: (_, attackBonus) =>
            {
            if (context.DeckSummary.FrontloadDamageSources >= 7 &&
                NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                attackBonus *= 0.55d;
            }

                return attackBonus;
            },
            weakAttackAdjust: (candidate, attackPenalty) =>
            {
            if (MatchesAny(card, "DEMON_FORM", "DemonForm", "恶魔形态") &&
                context.ActFloor >= 10 &&
                context.DeckSummary.ScalingSources <= 0)
            {
                attackPenalty *= 0.55d;
            }

                return attackPenalty;
            },
            premiumDefenseAdjust: (_, defenseBonus) =>
            {
            if (NeedsDefenseMoreThanDamage(context.DeckSummary))
            {
                defenseBonus += 6d;
            }

                return defenseBonus;
            },
            weakDefenseAdjust: (candidate, defensePenalty) =>
            {
            if (MatchesAny(candidate, "FEEL_NO_PAIN", "无惧疼痛") &&
                context.DeckSummary.ExhaustCards >= 3)
            {
                defensePenalty *= 0.45d;
            }

            if (MatchesAny(candidate, "BARRICADE", "Barricade", "壁垒") &&
                context.DeckSummary.QualityDefenseSources >= 5)
            {
                defensePenalty *= 0.6d;
            }

                return defensePenalty;
            });
    }

    public static int ScoreCombatAction(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        if (!IsIroncladCombatContext(context))
        {
            return 0;
        }

        int score = 0;
        if (MatchesAny(card, "THRASH", "痛殴", "RAMPAGE", "无情猛攻"))
        {
            int damage = card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
            int hits = Math.Max(card.GetDirectDamageHits(), damage > 0 ? 1 : 0);
            score += 55 + Math.Min(130, damage * 6);
            score += Math.Min(60, hits * 18);

            if (context.IsEliteOrBossCombat || context.HasSustainedAttackPressure)
            {
                score += 45;
            }

            if (context.EnemyCount == 1)
            {
                score += 20;
            }
        }

        if (MatchesAny(card, "BLOODLETTING", "放血") &&
            card.GetEnergyGainWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0)
        {
            int followUps = CountValuableFollowUpsAfterEnergyGain(context, action, card);
            if (followUps > 0)
            {
                score += 95 + Math.Min(followUps, 3) * 45;
                if (context.IncomingDamageAfterBlock > 0)
                {
                    score += 35;
                }

                if (context.IsEliteOrBossCombat || context.HasSustainedAttackPressure)
                {
                    score += 30;
                }
            }
        }

        return score;
    }

    private static int CountValuableFollowUpsAfterEnergyGain(
        DeterministicCombatContext context,
        AiLegalActionOption currentAction,
        ResolvedCardView currentCard)
    {
        int energyAfter = Math.Max(
            0,
            context.Energy - (currentAction.EnergyCost ?? 0) +
            currentCard.GetEnergyGainWithOrbEvoke(context.Actor, context.Energy, currentAction.EnergyCost ?? 0));
        return context.LegalActions.Count(candidate =>
        {
            if (string.Equals(candidate.ActionId, currentAction.ActionId, StringComparison.Ordinal) ||
                string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                (candidate.EnergyCost ?? 0) > energyAfter ||
                string.IsNullOrEmpty(candidate.CardInstanceId) ||
                !context.HandCardsByInstanceId.TryGetValue(candidate.CardInstanceId, out ResolvedCardView? followUp))
            {
                return false;
            }

            return followUp.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
                   followUp.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
                   followUp.GetSummonAmount() > 0 ||
                   followUp.GetEnemyVulnerableAmount() > 0 ||
                   followUp.GetEnemyWeakAmount() > 0 ||
                   followUp.GetSelfStrengthAmount() > 0 ||
                   followUp.GetSelfDexterityAmount() > 0 ||
                   followUp.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Power;
        });
    }

    private static bool IsIroncladContext(CardEvaluationContext context)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (characterId.Contains("IRONCLAD", StringComparison.Ordinal))
        {
            return true;
        }

        return context.DeckCards.Any(static card => MatchesAny(card, "STRIKE_IRONCLAD", "STRIKEIRONCLAD", "DEFEND_IRONCLAD", "DEFENDIRONCLAD", "BASH"));
    }

    private static bool IsIroncladCombatContext(DeterministicCombatContext context)
    {
        string characterId = Normalize(context.Actor.Character.Id.Entry);
        if (characterId.Contains("IRONCLAD", StringComparison.Ordinal))
        {
            return true;
        }

        return context.Actor.Deck.Cards.Any(card =>
            card.Id.Entry.Contains("IRONCLAD", StringComparison.OrdinalIgnoreCase) ||
            card.Id.Entry.Contains("BASH", StringComparison.OrdinalIgnoreCase));
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
