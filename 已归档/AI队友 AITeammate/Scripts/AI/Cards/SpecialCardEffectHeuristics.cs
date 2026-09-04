using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal static class SpecialCardEffectHeuristics
{
    public static int ScoreCombatAction(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int score = 0;
        score += ScoreUpgradeHand(context, action, card);
        score += ScoreSpecialEnemyDebuff(context, card);
        score += ScoreOrbChannel(context, card);
        score += ScoreSpecialPowerSetup(context, action, card);
        score += ScoreSearchOrGeneration(context, card);
        return score;
    }

    public static int ScoreLineSetup(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int score = 0;
        int upgradeTargets = CountUpgradableHandTargets(context, action, card);
        if (upgradeTargets > 0)
        {
            score += 70 + upgradeTargets * 22;
        }

        if (IsSpecialAllEnemyDebuff(card))
        {
            score += 85;
            if (context.IncomingDamage > 0)
            {
                score += Math.Min(140, context.IncomingDamage * 4);
            }
        }

        if (IsOrbChannelCard(card))
        {
            score += EstimateOrbChannelUtility(context, card) / 2;
        }

        if (IsSearchOrGenerationCard(card))
        {
            score += 36;
        }

        return score;
    }

    public static int EstimateCardSelectionUtility(ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int utility = 0;
        if (IsUpgradeHandCard(card))
        {
            utility += 16;
        }

        if (IsShockwave(card))
        {
            utility += 26;
        }
        else if (IsPiercingWail(card))
        {
            utility += 18;
        }

        if (IsOrbChannelCard(card))
        {
            utility += 12;
        }

        if (IsKnownUsefulPower(card))
        {
            utility += 14;
        }

        if (IsSearchOrGenerationCard(card))
        {
            utility += 10;
        }

        return utility;
    }

    public static bool HasKnownSpecialBenefit(ResolvedCardView? card)
    {
        return EstimateCardSelectionUtility(card) > 0 ||
               IsSpecialAllEnemyDebuff(card) ||
               IsUpgradeHandCard(card) ||
               card.HasOrbSemanticEffect() ||
               card.GetRecognizedUtilityAmount() > 0;
    }

    public static int GetSpecialEnemyVulnerableAmount(ResolvedCardView? card)
    {
        return IsShockwave(card) ? 3 : 0;
    }

    public static int GetSpecialEnemyWeakAmount(ResolvedCardView? card)
    {
        if (IsShockwave(card))
        {
            return 3;
        }

        return IsPiercingWail(card) ? 1 : 0;
    }

    public static bool AppliesSpecialDebuffToAllEnemies(ResolvedCardView? card)
    {
        return IsSpecialAllEnemyDebuff(card);
    }

    private static int ScoreUpgradeHand(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        if (!IsUpgradeHandCard(card))
        {
            return 0;
        }

        int targets = CountUpgradableHandTargets(context, action, card);
        if (targets <= 0)
        {
            return card.GetEstimatedBlock() > 0 ? 12 : -35;
        }

        int score = 105 + targets * 34;
        int premiumTargets = context.HandCardsByInstanceId.Values.Count(handCard =>
            IsUpgradableHandTarget(handCard, action, card) &&
            (handCard.Type == CardType.Power ||
             handCard.GetEstimatedDamage() >= 8 ||
             handCard.GetEstimatedBlock() >= 8 ||
             handCard.GetCardsDrawn() > 0 ||
             handCard.GetEnemyVulnerableAmount() > 0 ||
             handCard.GetEnemyWeakAmount() > 0 ||
             IsKnownUsefulPower(handCard)));
        score += Math.Min(4, premiumTargets) * 18;

        if (context.IsEliteOrBossCombat || context.HasSustainedAttackPressure)
        {
            score += 45;
        }

        if (context.IncomingDamageAfterBlock > 0 && card.GetEstimatedBlock() > 0)
        {
            score += Math.Min(80, context.IncomingDamageAfterBlock * 5);
        }

        return score;
    }

    private static int ScoreSpecialEnemyDebuff(DeterministicCombatContext context, ResolvedCardView card)
    {
        if (IsShockwave(card))
        {
            int score = 185 + Math.Min(context.EnemiesById.Count, 4) * 35;
            if (context.IsEliteOrBossCombat)
            {
                score += 70;
            }

            if (context.HasSustainedAttackPressure)
            {
                score += 45;
            }

            return score;
        }

        if (IsPiercingWail(card))
        {
            int attackingEnemies = context.EnemiesById.Values.Count(static enemy => enemy.IncomingDamage > 0);
            int score = 35 + attackingEnemies * 65;
            if (context.IncomingDamage > 0)
            {
                score += Math.Min(260, context.IncomingDamage * 8);
            }
            else
            {
                score -= 85;
            }

            return score;
        }

        return 0;
    }

    private static int ScoreOrbChannel(DeterministicCombatContext context, ResolvedCardView card)
    {
        if (!IsOrbChannelCard(card))
        {
            return 0;
        }

        return EstimateOrbChannelUtility(context, card);
    }

    private static int EstimateOrbChannelUtility(DeterministicCombatContext context, ResolvedCardView card)
    {
        int score = 0;
        if (card.MatchesCardToken("ZAP", "BALL_LIGHTNING", "LIGHTNING", "THUNDER", "TEMPEST"))
        {
            score += 90;
            if (context.EnemiesById.Count > 0)
            {
                score += 24;
            }
        }

        if (card.MatchesCardToken("CHILL", "COOLHEADED", "COLD_SNAP", "GLACIER", "FROST"))
        {
            score += context.IncomingDamageAfterBlock > 0
                ? 85 + Math.Min(145, context.IncomingDamageAfterBlock * 5)
                : 45;
        }

        if (card.MatchesCardToken("CHAOS", "RAINBOW", "DARKNESS", "FUSION", "PLASMA"))
        {
            score += context.IsEliteOrBossCombat ? 110 : 78;
        }

        return score;
    }

    private static int ScoreSpecialPowerSetup(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        if (!IsKnownUsefulPower(card))
        {
            return 0;
        }

        int score = card.Type == CardType.Power ? 0 : 40;
        if (card.MatchesCardToken("RAGE"))
        {
            int attacks = context.HandCardsByInstanceId.Values.Count(handCard =>
                !string.Equals(handCard.CardInstanceId, action.CardInstanceId, StringComparison.Ordinal) &&
                handCard.Type == CardType.Attack &&
                Math.Max(0, handCard.EffectiveCost) <= context.Energy);
            score += attacks > 0 ? 55 + attacks * 28 : -45;
        }

        if (card.MatchesCardToken("FEEL_NO_PAIN"))
        {
            int exhaustCards = context.HandCardsByInstanceId.Values.Count(static handCard => handCard.Exhaust);
            int burdens = StatusCardStrategy.CountAllowedHandCleanupTargets(context.HandCardsByInstanceId.Values, context.Actor);
            score += 65 + Math.Min(5, exhaustCards + burdens) * 25;
        }

        if (card.MatchesCardToken("JUGGERNAUT", "JUGGLING"))
        {
            int blockActions = context.HandCardsByInstanceId.Values.Count(static handCard =>
                handCard.GetEstimatedBlock() > 0 || handCard.GetSummonAmount() > 0);
            score += 55 + Math.Min(4, blockActions) * 22;
        }

        if (card.MatchesCardToken("ACCURACY", "INFINITE_BLADES", "NOXIOUS_FUMES", "BARRICADE", "LOOP", "STORM"))
        {
            score += context.IsEliteOrBossCombat ? 90 : 55;
        }

        return score;
    }

    private static int ScoreSearchOrGeneration(DeterministicCombatContext context, ResolvedCardView card)
    {
        if (!IsSearchOrGenerationCard(card))
        {
            return 0;
        }

        int score = 48;
        if (context.IsEliteOrBossCombat || context.HasSustainedAttackPressure)
        {
            score += 28;
        }

        if (context.Energy <= 0 && card.EffectiveCost > 0)
        {
            score -= 45;
        }

        return score;
    }

    private static int CountUpgradableHandTargets(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        if (!IsUpgradeHandCard(card))
        {
            return 0;
        }

        return context.HandCardsByInstanceId.Values.Count(handCard => IsUpgradableHandTarget(handCard, action, card));
    }

    private static bool IsUpgradableHandTarget(ResolvedCardView handCard, AiLegalActionOption action, ResolvedCardView sourceCard)
    {
        if (handCard.IsUpgraded ||
            StatusCardStrategy.IsNegativeStatusOrCurse(handCard) ||
            string.Equals(handCard.CardInstanceId, action.CardInstanceId, StringComparison.Ordinal))
        {
            return false;
        }

        return handCard.Type is CardType.Attack or CardType.Skill or CardType.Power ||
               handCard.GetEstimatedDamage() > 0 ||
               handCard.GetEstimatedProtection() > 0 ||
               SpecialCardEffectHeuristics.HasKnownSpecialBenefit(handCard);
    }

    private static bool IsUpgradeHandCard(ResolvedCardView? card)
    {
        return card.MatchesCardToken("ARMAMENTS", "ARMAMENT", "THE_SMITH", "APOTHEOSIS");
    }

    private static bool IsShockwave(ResolvedCardView? card)
    {
        return card.MatchesCardToken("SHOCKWAVE", "SHOCK_WAVE");
    }

    private static bool IsPiercingWail(ResolvedCardView? card)
    {
        return card.MatchesCardToken("PIERCING_WAIL");
    }

    private static bool IsSpecialAllEnemyDebuff(ResolvedCardView? card)
    {
        return IsShockwave(card) || IsPiercingWail(card);
    }

    private static bool IsOrbChannelCard(ResolvedCardView? card)
    {
        return card.MatchesCardToken(
            "ZAP",
            "BALL_LIGHTNING",
            "COLD_SNAP",
            "COOLHEADED",
            "CHILL",
            "GLACIER",
            "CHAOS",
            "RAINBOW",
            "DARKNESS",
            "FUSION",
            "TEMPEST",
            "THUNDER",
            "LIGHTNING",
            "FROST",
            "PLASMA");
    }

    private static bool IsKnownUsefulPower(ResolvedCardView? card)
    {
        return card.MatchesCardToken(
            "RAGE",
            "FEEL_NO_PAIN",
            "JUGGERNAUT",
            "JUGGLING",
            "ACCURACY",
            "INFINITE_BLADES",
            "NOXIOUS_FUMES",
            "BARRICADE",
            "LOOP",
            "STORM",
            "SERPENT_FORM");
    }

    private static bool IsSearchOrGenerationCard(ResolvedCardView? card)
    {
        return card.MatchesCardToken(
            "SECRET_TECHNIQUE",
            "SECRET_WEAPON",
            "DISCOVERY",
            "WHITE_NOISE",
            "MIMIC",
            "HIDDEN_GEM",
            "TRASH_TO_TREASURE");
    }
}
