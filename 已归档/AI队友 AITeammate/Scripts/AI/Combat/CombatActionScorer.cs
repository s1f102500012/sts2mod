using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class CombatActionScorer
{
    private const int SetupPotionWithFollowUpBonus = 90;
    private const int SetupPotionWithoutFollowUpPenalty = 120;
    private const int DirectPotionLethalBonus = 260;
    private const int FullPotionSlotsUseBonus = 145;
    private const int FullPotionSlotsEliteBossBonus = 35;
    private const int KnownFuturePotionDropFullSlotsUseBonus = 285;
    private const int LongFightScalingPotionBonus = 135;
    private const int EliteBossUtilityPotionBonus = 55;
    private const int HighPressureHandFixPotionBonus = 95;
    private const int ThornsPotionUnderAttackBonus = 85;
    private const int DuplicationPotionFollowUpBonus = 70;
    private const int ImmediateDefenseNoThreatPenalty = 720;
    private const int ImmediateDefensePreventedDamageValue = 22;
    private const int ImmediateDefenseCriticalTargetBonus = 150;
    private const int PersistentDefenseNoNeedPenalty = 90;
    private const int FriendlyTempoWrongTargetPenalty = 80;
    private const int FriendlyTempoSelfTargetBonus = 18;
    private const int UnclassifiedPotionUsePenalty = 135;
    private const int UnknownPotionEmergencyUseBonus = 215;
    private const int KnownPotionTeamCrisisUseBonus = 125;
    private const int SevereIncomingPotionUseBonus = 95;
    private const int MeaninglessTemporaryPotionUsePenalty = 980;
    private const int NormalFightPotionConservationPenalty = 150;
    private const int NormalFightScalingPotionConservationPenalty = 235;
    private const int MultiEnemyKillRemovalBonus = 130;
    private const int MultiEnemyNearKillProgressBonus = 70;
    private const int BlockedTargetMultiEnemyMinPenaltyPerPoint = 2;
    private const int BlockedTargetMultiEnemyHpOpportunityDivisor = 4;
    private const int TeamFocusedKillTargetBonus = 260;
    private const int TeamFocusedKillDamageValuePerPoint = 12;
    private const int TeamFocusedKillOffTargetPenalty = 210;
    private const int TeamFocusedKillNonDamagePenalty = 105;
    private const int TeamFocusedKillDebuffBonus = 90;
    private const int TeamFocusedKillImmediateDefenseDrag = 160;
    private const int NonMinionLethalTargetBonus = 360;
    private const int NonMinionLethalDamageValuePerPoint = 14;
    private const int NonMinionLethalMinionTargetPenalty = 640;
    private const int NonMinionLethalNonDamagePenalty = 120;
    private const int GardenersTargetLockTargetBonus = 230;
    private const int GardenersTargetLockDamageValuePerPoint = 15;
    private const int GardenersTargetLockOffTargetPenalty = 330;
    private const int GardenersTargetLockNonDamagePenalty = 95;
    private const int GardenersLowHpFocusBonus = 165;
    private const int ObscuraTargetLockTargetBonus = 460;
    private const int ObscuraTargetLockDamageValuePerPoint = 22;
    private const int ObscuraTargetLockOffTargetPenalty = 820;
    private const int ObscuraTargetLockNonDamagePenalty = 650;
    private const int ObscuraPrimaryLethalBonus = 1400;
    private const int ObscuraLowHpFocusBonus = 280;
    private const int LowConfidenceNoBenefitBasePenalty = 45;
    private const int CatastrophicRaceNonDamagePenalty = 240;
    private const int IntangibleNonLethalAttackPenalty = 150;
    private const int ThornsNonLethalAttackBasePenalty = 95;
    private const int ThornsReturnedDamageValue = 24;
    private const int ThornsMultiHitExtraPenalty = 34;
    private const int SummonIntentLethalBonus = 520;
    private const int SummonedAddLethalBonus = 620;
    private const int SummonedAddPressureBonus = 185;
    private const int OverwhelmedByAddsSummonerPenalty = 190;
    private const int KaiserCrabTurnLeverBonus = 260;
    private const int SafePowerSetupBonus = 150;
    private const int EliteBossPowerSetupBonus = 75;
    private const int UnknownPowerSetupFloorBonus = 95;
    private const int WaterfallSelfDestructBlockBonus = 430;
    private const int WaterfallSelfDestructNonLethalAttackPenalty = 760;
    private const int WaterfallSelfDestructLethalBonus = 1600;
    private const int LagavulinSleepSetupBonus = 420;
    private const int LagavulinSleepDebuffBonus = 260;
    private const int LagavulinSleepBlockChipPenalty = 950;
    private const int LagavulinSleepWakePenalty = 5200;
    private const int LagavulinOpeningForbiddenActionPenalty = 100000;
    private const int CorpseSlugDebuffIntentFocusBonus = 1150;
    private const int CorpseSlugAttackerFirstPenalty = 980;

    public CombatActionScore Score(DeterministicCombatContext context, AiLegalActionOption action)
    {
        AiCharacterCombatTuning tuning = context.CombatConfig.Combat;
        AiCombatRiskProfile risk = tuning.RiskProfile;

        if (string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal))
        {
            return new CombatActionScore
            {
                ActionId = action.ActionId,
                Category = CombatActionCategory.EndTurn,
                TotalScore = ScoreEndTurn(context)
            };
        }

        if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
        {
            if (LagavulinMatriarchStrategy.IsForbiddenOpeningAction(context, action, null))
            {
                return new CombatActionScore
                {
                    ActionId = action.ActionId,
                    Category = CombatActionCategory.Potion,
                    TotalScore = -LagavulinOpeningForbiddenActionPenalty
                };
            }

            return new CombatActionScore
            {
                ActionId = action.ActionId,
                Category = CombatActionCategory.Potion,
                TotalScore = ScorePotion(context, action)
            };
        }

        ResolvedCardView? card = ResolveCard(context, action);
        if (LagavulinMatriarchStrategy.IsForbiddenOpeningAction(context, action, card))
        {
            return new CombatActionScore
            {
                ActionId = action.ActionId,
                Category = CombatActionCategory.Utility,
                TotalScore = -LagavulinOpeningForbiddenActionPenalty
            };
        }

        if (card == null)
        {
            return new CombatActionScore
            {
                ActionId = action.ActionId,
                Category = CombatActionCategory.Utility,
                TotalScore = ScoreUtility(context, action)
            };
        }

        CombatActionScoreBreakdown breakdown = BuildScoreBreakdown(context, action, card);
        int totalScore = breakdown.WeightedTotal(risk);
        CombatActionCategory category = Classify(
            context,
            action,
            card,
            breakdown.ImmediateDamage,
            breakdown.ImmediateDefense,
            breakdown.SelfBuff,
            breakdown.ResourceSetup);
        Log.Debug(
            $"[AITeammate] Semantic score actionId={action.ActionId} category={category} pressure={context.SustainedAttackPressure} catastrophic={context.HasCatastrophicEnemyAction} {breakdown.Describe()} total={totalScore}");

        return new CombatActionScore
        {
            ActionId = action.ActionId,
            Category = category,
            TotalScore = totalScore
        };
    }

    private static CombatActionScoreBreakdown BuildScoreBreakdown(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card)
    {
        int characterStrategyScore = RegentCharacterStrategy.ScoreCombatAction(context, action, card) +
                                     IroncladCharacterStrategy.ScoreCombatAction(context, action, card) +
                                     SpecialCardEffectHeuristics.ScoreCombatAction(context, action, card);
        return new CombatActionScoreBreakdown(
            ImmediateDamage: ScoreImmediateDamage(context, action, card),
            ImmediateDefense: ScoreImmediateDefense(context, action, card),
            EnemyDebuff: ScoreEnemyDebuff(context, action, card),
            SelfBuff: ScoreSelfBuff(context, action, card),
            ResourceSetup: ScoreResourceSetup(context, action, card),
            StatusCleanup: ScoreStatusCleanup(context, card),
            KillPotential: ScoreKillPotential(context, action, card),
            CharacterStrategy: characterStrategyScore,
            TeamCoordination: ScoreTeamCoordination(context, action, card),
            NonMinionLethal: ScoreNonMinionLethalPriority(context, action, card),
            LagavulinSleep: ScoreLagavulinMatriarchSleepAction(context, action, card),
            NoBenefit: ScoreLowConfidenceNoBenefitPenalty(context, action, card),
            EnergyEfficiency: ScoreEnergyEfficiency(context, action));
    }

    private static CombatActionCategory Classify(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card,
        int immediateDamageScore,
        int immediateDefenseScore,
        int selfBuffScore,
        int resourceSetupScore)
    {
        OrbEvokeEstimate evoke = card.EstimateOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        if (immediateDefenseScore >= Math.Max(immediateDamageScore, selfBuffScore) &&
            (card.HasEffect(EffectKind.GainBlock) || card.HasEffect(EffectKind.Summon) || evoke.Block > 0))
        {
            return CombatActionCategory.Block;
        }

        if (immediateDamageScore > 0 &&
            (card.HasEffect(EffectKind.DealDamage) || evoke.Damage > 0 || card.Type == CardType.Attack))
        {
            return CombatActionCategory.Attack;
        }

        if (selfBuffScore >= resourceSetupScore && card.Type == CardType.Power)
        {
            return CombatActionCategory.PowerSetup;
        }

        if (resourceSetupScore > 0)
        {
            return CombatActionCategory.Utility;
        }

        return CombatActionCategory.Utility;
    }

    private static ResolvedCardView? ResolveCard(DeterministicCombatContext context, AiLegalActionOption action)
    {
        if (!string.IsNullOrEmpty(action.CardInstanceId) &&
            context.HandCardsByInstanceId.TryGetValue(action.CardInstanceId, out ResolvedCardView? liveCard))
        {
            return liveCard;
        }

        return null;
    }

    private static int ScoreImmediateDamage(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        AiCombatCoreWeights core = context.CombatConfig.Combat.CoreWeights;
        AiCombatStatusWeights status = context.CombatConfig.Combat.StatusWeights;
        OrbEvokeEstimate evoke = card.EstimateOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        int directDamage = card.GetEstimatedDamage();
        int damage = directDamage + evoke.Damage;
        if (damage <= 0)
        {
            return 0;
        }

        int directHits = Math.Max(0, GetDamageHits(card));
        int hits = Math.Max(1, directHits + evoke.DamageHits);
        int punishableAttackHits = card.Type == CardType.Attack && directDamage > 0
            ? Math.Max(1, directHits)
            : 0;
        int score = 0;
        if (card.DealsDamageToAllEnemies())
        {
            foreach (DeterministicEnemyState enemy in context.EnemiesById.Values)
            {
                score += ScoreDamageAgainstEnemy(context, enemy, damage, hits, punishableAttackHits);
            }
        }
        else if (!string.IsNullOrEmpty(action.TargetId) &&
            context.EnemiesById.TryGetValue(action.TargetId, out DeterministicEnemyState? enemy))
        {
            score += ScoreDamageAgainstEnemy(context, enemy, damage, hits, punishableAttackHits);
        }
        else if (context.EnemiesById.Count == 1)
        {
            score += ScoreDamageAgainstEnemy(context, context.EnemiesById.Values.First(), damage, hits, punishableAttackHits);
        }
        else
        {
            score += damage * core.DirectDamageValuePerPoint;
        }

        int uncoveredDamage = context.IncomingDamageAfterBlock;
        if (uncoveredDamage > 0 && HasPlayableBlockAction(context) && !context.HasCatastrophicEnemyAction)
        {
            score -= core.AttackWhileDefenseNeededPenalty;
        }

        score += GetActorPowerAmount(context, "STRENGTH") * directHits * status.StrengthPerHitValue;
        score += card.GetSelfTemporaryStrengthAmount() * status.SelfTemporaryStrengthValue;
        return score;
    }

    private static int ScoreDamageAgainstEnemy(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int rawDamage,
        int hits,
        int punishableAttackHits)
    {
        AiCombatCoreWeights core = context.CombatConfig.Combat.CoreWeights;
        int effectiveHp = enemy.CurrentHp + enemy.Block;
        int adjustedDamage = rawDamage;
        if (enemy.HasVulnerable)
        {
            adjustedDamage += (int)Math.Ceiling(adjustedDamage * 0.5m);
        }

        int effectiveDamage = EstimateEffectiveDamageAgainstEnemy(enemy, adjustedDamage, hits);
        int usefulDamage = Math.Min(effectiveDamage, effectiveHp);
        int overkillDamage = Math.Max(0, effectiveDamage - effectiveHp);
        int score = usefulDamage * core.DirectDamageValuePerPoint;
        score -= overkillDamage * Math.Max(1, core.DirectDamageValuePerPoint / 2);
        score += Math.Max(0, core.TargetLowHealthBiasThreshold - effectiveHp) * core.TargetLowHealthBiasValuePerPoint;
        score += EstimateSustainedAttackRaceDamageBonus(context, enemy, usefulDamage, adjustedDamage, effectiveHp);
        score += EstimateCatastrophicRaceDamageBonus(context, enemy, usefulDamage, adjustedDamage, effectiveHp);
        score += EstimateWaterfallSelfDestructDamageAdjustment(context, enemy, usefulDamage, adjustedDamage, effectiveDamage, effectiveHp);
        score += EstimatePhantasmalGardenersFocusDamageBonus(context, enemy, usefulDamage, adjustedDamage, effectiveHp);
        score += EstimateObscuraBodyFocusDamageBonus(context, enemy, usefulDamage, effectiveDamage, effectiveHp);
        score += EstimateSummonIntentDamageBonus(enemy, usefulDamage, effectiveDamage, effectiveHp);
        score += EstimateSummonedAddDamageBonus(context, enemy, usefulDamage, effectiveDamage, effectiveHp);
        score += EstimateKaiserCrabDamageBonus(context, enemy, usefulDamage, effectiveDamage, effectiveHp);
        score += EstimateCorpseSlugDebuffFocusBonus(context, enemy, usefulDamage, effectiveDamage, effectiveHp);
        score -= EstimateIntangibleAttackPenalty(context, enemy, adjustedDamage, effectiveDamage, effectiveHp);

        if (effectiveDamage >= effectiveHp)
        {
            score += Math.Max(10, core.TargetLowHealthBiasThreshold - Math.Min(effectiveHp, core.TargetLowHealthBiasThreshold));
            if (context.EnemiesById.Count > 1)
            {
                score += MultiEnemyKillRemovalBonus;
                score += Math.Min(130, enemy.IncomingDamage * 4 + enemy.SustainedAttackPressure);
            }
        }
        else if (context.EnemiesById.Count > 1 &&
                 enemy.Block <= 0 &&
                 enemy.CurrentHp <= Math.Max(effectiveDamage * 2, core.TargetLowHealthBiasThreshold))
        {
            score += Math.Min(
                95,
                usefulDamage * 3 + Math.Min(MultiEnemyNearKillProgressBonus, enemy.SustainedAttackPressure / 3));
        }

        if (ShouldPenalizeBlockedTarget(context, enemy, adjustedDamage, effectiveHp))
        {
            score -= EstimateBlockedTargetPenalty(core, enemy, adjustedDamage);
        }

        if (enemy.IsAttacking)
        {
            score += core.AttackingTargetBonus;
        }

        if (enemy.HasVulnerable)
        {
            score += Math.Max(4, usefulDamage * core.DirectDamageValuePerPoint / 2);
        }

        if (enemy.HasWeak)
        {
            score += 4;
        }

        if (enemy.HasBuffer)
        {
            score -= 18;
        }

        score -= EstimateThornsAttackPenalty(context, enemy, punishableAttackHits, effectiveDamage, effectiveHp);
        return score;
    }

    private static int EstimateThornsAttackPenalty(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int punishableAttackHits,
        int effectiveDamage,
        int effectiveHp)
    {
        if (!enemy.PunishesAttacks || punishableAttackHits <= 0)
        {
            return 0;
        }

        int returnedDamage = enemy.PunishingAttackAmount * punishableAttackHits;
        bool lethal = effectiveDamage >= effectiveHp;
        int penalty = returnedDamage * ThornsReturnedDamageValue;
        if (context.CurrentHp <= returnedDamage + 8 || context.IsTeamInCrisis)
        {
            penalty += returnedDamage * 18;
        }

        if (punishableAttackHits > 1)
        {
            penalty += (punishableAttackHits - 1) * ThornsMultiHitExtraPenalty;
        }

        if (!lethal)
        {
            penalty += ThornsNonLethalAttackBasePenalty;
            if (context.EnemiesById.Values.Any(static candidate => !candidate.PunishesAttacks))
            {
                penalty += 75;
            }
        }
        else
        {
            penalty = Math.Max(0, penalty / 3 - 45);
        }

        return penalty;
    }

    private static int EstimateCorpseSlugDebuffFocusBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (usefulDamage <= 0 ||
            !ShouldFocusCorpseSlugDebuffIntent(context))
        {
            return 0;
        }

        if (enemy.IsCorpseSlugDebuffIntent)
        {
            int score = CorpseSlugDebuffIntentFocusBonus + Math.Min(520, usefulDamage * 26);
            if (effectiveDamage >= effectiveHp)
            {
                score += 900;
            }

            return score;
        }

        if (enemy.IsCorpseSlug && enemy.IsAttacking)
        {
            return -(CorpseSlugAttackerFirstPenalty + Math.Min(520, usefulDamage * 22));
        }

        return 0;
    }

    private static int ScoreLagavulinMatriarchSleepAction(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card)
    {
        if (!context.IsLagavulinMatriarchOpeningSetupWindow)
        {
            return 0;
        }

        int damage = EstimateCardDamageAgainstSleepingLagavulin(context, action, card);
        if (damage > 0)
        {
            DeterministicEnemyState? matriarch = context.EnemiesById.Values.FirstOrDefault(static enemy => enemy.IsLagavulinMatriarchAsleep);
            int block = Math.Max(0, matriarch?.Block ?? 0);
            if (damage > block)
            {
                return -(LagavulinSleepWakePenalty + Math.Min(1800, (damage - block) * 120));
            }

            return -(LagavulinSleepBlockChipPenalty + Math.Min(900, damage * 55));
        }

        int score = 0;
        bool isSetupCard = card.Type is CardType.Power or CardType.Skill;
        if (isSetupCard)
        {
            score += LagavulinSleepSetupBonus;
        }

        if (LagavulinMatriarchStrategy.HasEnemyDebuff(card))
        {
            score += LagavulinSleepDebuffBonus;
        }

        if (card.GetSelfStrengthAmount() > 0 ||
            card.GetSelfDexterityAmount() > 0 ||
            card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0 ||
            card.GetSummonAmount() > 0 ||
            card.GetCardsDrawn() > 0 ||
            card.GetEnergyGain() > 0 ||
            SpecialCardEffectHeuristics.HasKnownSpecialBenefit(card))
        {
            score += LagavulinSleepSetupBonus / 2;
        }

        return score;
    }

    private static int EstimateCardDamageAgainstSleepingLagavulin(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card)
    {
        DeterministicEnemyState? matriarch = context.EnemiesById.Values.FirstOrDefault(static enemy => enemy.IsLagavulinMatriarchAsleep);
        if (matriarch == null)
        {
            return 0;
        }

        bool targetsMatriarch = card.DealsDamageToAllEnemies() ||
                                string.Equals(action.TargetId, matriarch.Id, StringComparison.Ordinal) ||
                                (context.EnemiesById.Count == 1 && string.IsNullOrEmpty(action.TargetId));
        if (!targetsMatriarch)
        {
            return 0;
        }

        OrbEvokeEstimate evoke = card.EstimateOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        int damage = card.GetEstimatedDamage() + evoke.Damage;
        if (damage <= 0)
        {
            return 0;
        }

        int directHits = Math.Max(0, GetDamageHits(card));
        int hits = Math.Max(1, directHits + evoke.DamageHits);
        int adjustedDamage = damage + GetActorPowerAmount(context, "STRENGTH") * directHits;
        if (matriarch.HasVulnerable)
        {
            adjustedDamage += (int)Math.Ceiling(adjustedDamage * 0.5m);
        }

        return EstimateEffectiveDamageAgainstEnemy(matriarch, adjustedDamage, hits);
    }

    private static int EstimateWaterfallSelfDestructDamageAdjustment(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int adjustedDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (!context.IsWaterfallSelfDestructDefenseWindow || !enemy.IsWaterfallGiant || usefulDamage <= 0)
        {
            return 0;
        }

        if (effectiveDamage >= effectiveHp)
        {
            return WaterfallSelfDestructLethalBonus;
        }

        int wastedRaceDamage = Math.Max(0, adjustedDamage - usefulDamage);
        return -(WaterfallSelfDestructNonLethalAttackPenalty + Math.Min(520, usefulDamage * 16 + wastedRaceDamage * 5));
    }

    internal static int EstimateEffectiveDamageAgainstEnemy(DeterministicEnemyState enemy, int adjustedDamage, int hits)
    {
        int damage = Math.Max(0, adjustedDamage);
        if (!enemy.HasIntangible || damage <= 0)
        {
            return damage;
        }

        return Math.Min(damage, Math.Max(1, hits));
    }

    private static int EstimateIntangibleAttackPenalty(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int adjustedDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (!enemy.HasIntangible || effectiveDamage >= effectiveHp)
        {
            return 0;
        }

        int wastedDamage = Math.Max(0, adjustedDamage - effectiveDamage);
        int penalty = IntangibleNonLethalAttackPenalty + Math.Min(180, wastedDamage * 6);
        if (context.EnemiesById.Values.Any(candidate => !candidate.HasIntangible))
        {
            penalty += 85;
        }

        return penalty;
    }

    private static int EstimateSummonIntentDamageBonus(
        DeterministicEnemyState enemy,
        int usefulDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (!enemy.HasSummonMove || usefulDamage <= 0)
        {
            return 0;
        }

        int score = Math.Min(180, usefulDamage * 12);
        if (effectiveDamage >= effectiveHp)
        {
            score += SummonIntentLethalBonus;
        }

        return score;
    }

    private static int EstimateSummonedAddDamageBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (usefulDamage <= 0)
        {
            return 0;
        }

        int dangerousAddCount = context.EnemiesById.Values.Count(static candidate =>
            candidate.IsLikelySummonedAdd &&
            (candidate.IsAttacking || candidate.SustainedAttackPressure > 0));
        int dangerousAddIncoming = context.EnemiesById.Values
            .Where(static candidate => candidate.IsLikelySummonedAdd)
            .Sum(static candidate => candidate.IncomingDamage);

        if (enemy.IsLikelySummonedAdd)
        {
            int score = Math.Min(260, usefulDamage * 14);
            score += Math.Min(220, enemy.IncomingDamage * 12 + enemy.SustainedAttackPressure * 2);
            if (context.IsTeamInCrisis || dangerousAddIncoming >= 18)
            {
                score += SummonedAddPressureBonus;
            }

            if (effectiveDamage >= effectiveHp)
            {
                score += SummonedAddLethalBonus + Math.Min(260, enemy.IncomingDamage * 18);
            }

            if (enemy.Block > 0)
            {
                score += Math.Min(140, enemy.Block * 4);
            }

            return score;
        }

        if (enemy.IsActiveSummoner && dangerousAddCount >= 2 && effectiveDamage < effectiveHp)
        {
            return -OverwhelmedByAddsSummonerPenalty - Math.Min(180, dangerousAddIncoming * 8);
        }

        return 0;
    }

    private static int EstimateKaiserCrabDamageBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (!context.IsKaiserCrabCombat || !enemy.IsKaiserCrabPart || usefulDamage <= 0)
        {
            return 0;
        }

        if (effectiveDamage >= effectiveHp)
        {
            return 680;
        }

        DeterministicEnemyState? otherHeavyAttacker = context.EnemiesById.Values
            .Where(candidate => !string.Equals(candidate.Id, enemy.Id, StringComparison.Ordinal) && candidate.IsKaiserCrabPart)
            .OrderByDescending(static candidate => candidate.IncomingDamage)
            .FirstOrDefault();
        if (enemy.IsKaiserCrabTurnLeverMove && otherHeavyAttacker is { IncomingDamage: >= 16 })
        {
            return KaiserCrabTurnLeverBonus + Math.Min(160, usefulDamage * 10);
        }

        bool anotherLeverAvailable = context.EnemiesById.Values.Any(candidate =>
            !string.Equals(candidate.Id, enemy.Id, StringComparison.Ordinal) &&
            candidate.IsKaiserCrabPart &&
            candidate.IsKaiserCrabTurnLeverMove);
        if (enemy.IsKaiserCrabHeavyAttackMove && anotherLeverAvailable)
        {
            return -150;
        }

        return 0;
    }

    private static int EstimatePhantasmalGardenersFocusDamageBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int adjustedDamage,
        int effectiveHp)
    {
        if (!context.IsPhantasmalGardenersCombat ||
            !string.Equals(enemy.Id, context.TeamTactics.PrimaryTargetId, StringComparison.Ordinal) ||
            usefulDamage <= 0)
        {
            return 0;
        }

        int score = GardenersTargetLockTargetBonus / 2;
        score += Math.Min(260, usefulDamage * 9);
        if (enemy.CurrentHp <= 30)
        {
            score += Math.Max(20, GardenersLowHpFocusBonus - enemy.CurrentHp * 3);
        }

        if (enemy.Block > 0 && adjustedDamage < effectiveHp)
        {
            score += Math.Min(120, enemy.Block * 3);
        }

        return score;
    }

    private static int EstimateObscuraBodyFocusDamageBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int effectiveDamage,
        int effectiveHp)
    {
        if (!context.IsObscuraCombat || usefulDamage <= 0)
        {
            return 0;
        }

        bool isPrimaryBody =
            enemy.IsObscuraBody ||
            string.Equals(enemy.Id, context.TeamTactics.PrimaryTargetId, StringComparison.Ordinal);
        if (!isPrimaryBody)
        {
            return -(ObscuraTargetLockOffTargetPenalty + Math.Min(460, usefulDamage * 20));
        }

        int score = ObscuraTargetLockTargetBonus;
        score += Math.Min(620, usefulDamage * ObscuraTargetLockDamageValuePerPoint);
        if (effectiveDamage >= effectiveHp)
        {
            score += ObscuraPrimaryLethalBonus;
        }

        if (enemy.CurrentHp <= 40)
        {
            score += Math.Max(60, ObscuraLowHpFocusBonus - enemy.CurrentHp * 4);
        }

        if (enemy.Block > 0 && effectiveDamage < effectiveHp)
        {
            score += Math.Min(180, enemy.Block * 4);
        }

        return score;
    }

    private static int EstimateSustainedAttackRaceDamageBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int adjustedDamage,
        int effectiveHp)
    {
        int pressure = enemy.SustainedAttackPressure;
        if (pressure <= 0 || usefulDamage <= 0)
        {
            return 0;
        }

        int perDamageBonus = Math.Clamp(pressure / 18, 2, 8);
        int score = usefulDamage * perDamageBonus;

        if (enemy.IsAttacking)
        {
            score += Math.Min(45, pressure / 3);
        }

        if (adjustedDamage >= effectiveHp)
        {
            score += Math.Min(110, pressure);
        }

        if (context.EnemiesById.Count > 1)
        {
            score += Math.Min(35, pressure / 4);
        }

        return score;
    }

    private static int EstimateCatastrophicRaceDamageBonus(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int usefulDamage,
        int adjustedDamage,
        int effectiveHp)
    {
        if (!context.HasCatastrophicEnemyAction || usefulDamage <= 0)
        {
            return 0;
        }

        int score = usefulDamage * (enemy.HasCatastrophicMove ? 18 : 10);
        if (enemy.HasCatastrophicMove)
        {
            score += 160;
        }

        if (adjustedDamage >= effectiveHp)
        {
            score += enemy.HasCatastrophicMove ? 900 : 360;
        }

        return score;
    }

    private static bool ShouldPenalizeBlockedTarget(
        DeterministicCombatContext context,
        DeterministicEnemyState enemy,
        int adjustedDamage,
        int effectiveHp)
    {
        if ((context.IsPhantasmalGardenersCombat || context.IsObscuraCombat) &&
            string.Equals(enemy.Id, context.TeamTactics.PrimaryTargetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (enemy.IsLikelySummonedAdd && enemy.CurrentHp <= 45)
        {
            return false;
        }

        return context.EnemiesById.Count > 1 &&
               enemy.Block > 0 &&
               adjustedDamage < effectiveHp;
    }

    private static int EstimateBlockedTargetPenalty(
        AiCombatCoreWeights core,
        DeterministicEnemyState enemy,
        int adjustedDamage)
    {
        int blockAbsorbed = Math.Min(enemy.Block, Math.Max(adjustedDamage, 0));
        int blockTax = blockAbsorbed * Math.Max(BlockedTargetMultiEnemyMinPenaltyPerPoint, core.DirectDamageValuePerPoint / 2);
        int opportunityTax = Math.Min(enemy.Block, Math.Max(1, enemy.CurrentHp / BlockedTargetMultiEnemyHpOpportunityDivisor));
        return blockTax + opportunityTax;
    }

	    private static int ScoreImmediateDefense(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
	    {
	        AiCombatStatusWeights status = context.CombatConfig.Combat.StatusWeights;
	        AiCombatRiskProfile risk = context.CombatConfig.Combat.RiskProfile;
	        DeterministicPlayerState? playerTarget = ResolvePlayerTarget(context, action);
	        int targetIncomingDamage = playerTarget?.IncomingDamage ?? context.IncomingDamage;
	        int targetBlock = playerTarget?.Block ?? context.CurrentBlock;
	        int targetHp = playerTarget?.CurrentHp ?? context.CurrentHp;
	        int uncoveredDamage = Math.Max(0, targetIncomingDamage - targetBlock);
	        int block = card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        int summonProtection = card.GetSummonAmount();
        int totalProtection = block + summonProtection;
        int weakAmount = card.GetEnemyWeakAmount();
        int temporaryDexterity = card.GetSelfTemporaryDexterityAmount();
        int dexterity = Math.Max(0, card.GetSelfDexterityAmount() - temporaryDexterity);
        int weakPrevention = card.AppliesWeakToAllEnemies()
            ? EstimateAllEnemyWeakPrevention(context, weakAmount)
            : EstimateWeakPrevention(context, action, weakAmount);
        int blockedDamage = Math.Min(totalProtection, uncoveredDamage);

        int score = 0;
	        if (totalProtection > 0)
	        {
	            score += blockedDamage * risk.BlockedDamageValuePerPoint;
	            score += Math.Max(0, totalProtection - uncoveredDamage) * risk.ExcessBlockValuePerPoint;
            score += summonProtection * 7;
            if (context.IsWaterfallSelfDestructDefenseWindow)
            {
                score += WaterfallSelfDestructBlockBonus +
                         totalProtection * 32 +
                         Math.Min(360, targetIncomingDamage * 5);
            }

	            if (uncoveredDamage > 0 && totalProtection >= uncoveredDamage)
	            {
	                score += risk.FullBlockCoverageBonus;
	            }

	            score += ScoreFriendlyCardTargeting(context, playerTarget, block, blockedDamage);
	        }

        if (weakPrevention > 0)
        {
            score += weakPrevention * status.WeakImmediateDefenseValue;
        }

        if (temporaryDexterity > 0)
        {
            int nearTermBlockValue = HasAffordableBlockFollowUp(context, action)
                ? status.TemporaryDexterityWithFollowUpBlockValue
                : (uncoveredDamage > 0 ? status.TemporaryDexterityThreatenedBlockValue : status.TemporaryDexteritySafeBlockValue);
            score += temporaryDexterity * nearTermBlockValue;
        }

        if (dexterity > 0)
        {
            int futureBlockValue = HasPlayableBlockAction(context)
                ? status.PersistentDexterityWithBlockValue
                : status.PersistentDexterityWithoutBlockValue;
            score += dexterity * futureBlockValue;
        }

	        if (targetHp <= Math.Max(12, targetIncomingDamage))
	        {
	            score += risk.LowHealthEmergencyDefenseBonus;
	        }

	        score -= EstimateSustainedAttackDefenseDrag(context, card, block, uncoveredDamage);
	        score -= EstimateCatastrophicDefenseDrag(context, card, block, uncoveredDamage);
	        return score;
	    }

    private static int ScoreFriendlyCardTargeting(
        DeterministicCombatContext context,
        DeterministicPlayerState? target,
        int block,
        int blockedDamage)
    {
        if (target == null || block <= 0)
        {
            return 0;
        }

        int targetNeed = EstimateFriendlyBlockNeed(target);
        int bestNeed = context.PlayerStatesById.Values
            .Select(EstimateFriendlyBlockNeed)
            .DefaultIfEmpty(0)
            .Max();
        int score = 0;
        if (blockedDamage > 0)
        {
            score += Math.Min(120, blockedDamage * 12);
        }
        else
        {
            score += Math.Min(40, target.MissingHp / 2);
        }

        if (target.IsInGraveDanger)
        {
            score += 170;
        }

        if (!target.IsActor && targetNeed > 0)
        {
            score += Math.Min(95, targetNeed * 6);
        }

        if (bestNeed > targetNeed + 2)
        {
            score -= Math.Min(260, (bestNeed - targetNeed) * 18);
        }

        return score;
    }

    private static int EstimateFriendlyBlockNeed(DeterministicPlayerState player)
    {
        int need = player.IncomingDamageAfterBlock * 2;
        if (player.IsInGraveDanger)
        {
            need += 24;
        }

        need += Math.Min(18, player.MissingHp / 2);
        return need;
    }

	    private static int EstimateSustainedAttackDefenseDrag(
        DeterministicCombatContext context,
        ResolvedCardView card,
        int block,
        int uncoveredDamage)
    {
        if (block <= 0 ||
            !context.HasSustainedAttackPressure ||
            IsGraveDanger(context) ||
            card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, 0) > 0 ||
            card.Type == CardType.Attack)
        {
            return 0;
        }

        int damageTakenAfterBlock = Math.Max(0, uncoveredDamage - block);
        int hpAfterBlock = context.CurrentHp - damageTakenAfterBlock;
        int safeReserve = EstimateSustainedAttackRaceHpReserve(context);
        if (hpAfterBlock <= safeReserve)
        {
            return 0;
        }

        int pressure = context.SustainedAttackPressure;
        int fullCoverageDrag = uncoveredDamage > 0 && block >= uncoveredDamage
            ? Math.Min(40, pressure / 3)
            : 0;
        int excessBlockDrag = Math.Max(0, block - uncoveredDamage) * 2;
        int pureDefenseDrag = Math.Min(55, pressure / 4);
        return Math.Min(110, fullCoverageDrag + excessBlockDrag + pureDefenseDrag);
    }

    private static int EstimateCatastrophicDefenseDrag(
        DeterministicCombatContext context,
        ResolvedCardView card,
        int block,
        int uncoveredDamage)
    {
        if (block <= 0 ||
            context.IsWaterfallSelfDestructDefenseWindow ||
            !context.HasCatastrophicEnemyAction ||
            card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, 0) > 0 ||
            card.GetEnemyVulnerableAmount() > 0 ||
            card.GetEnemyWeakAmount() > 0)
        {
            return 0;
        }

        int hpAfterBlock = context.CurrentHp - Math.Max(0, uncoveredDamage - block);
        if (hpAfterBlock <= Math.Max(6, context.CurrentHp / 5))
        {
            return 0;
        }

        int excessBlock = Math.Max(0, block - uncoveredDamage);
        return Math.Min(260, 90 + context.SustainedAttackPressure / 2 + excessBlock * 3);
    }

    private static int ScoreEnemyDebuff(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        AiCombatStatusWeights status = context.CombatConfig.Combat.StatusWeights;
        int score = 0;
        int vulnerable = card.GetEnemyVulnerableAmount();
        int weak = card.GetEnemyWeakAmount();
        int poison = card.GetEnemyPoisonAmount();

        if (vulnerable > 0)
        {
            int followUpAttacks = CountAffordableAttackActions(context, action);
            int targetMultiplier = card.AppliesVulnerableToAllEnemies()
                ? Math.Max(context.EnemiesById.Count, 1)
                : 1;
            score += vulnerable * targetMultiplier * (followUpAttacks > 0 ? status.VulnerableWithFollowUpValue : status.VulnerableWithoutFollowUpValue);
        }

        if (weak > 0)
        {
            int prevention = card.AppliesWeakToAllEnemies()
                ? EstimateAllEnemyWeakPrevention(context, weak)
                : EstimateWeakPrevention(context, action, weak);
            score += prevention * status.WeakDebuffValue;
        }

        if (poison > 0)
        {
            int targetMultiplier = card.AppliesPoisonToAllEnemies()
                ? Math.Max(context.EnemiesById.Count, 1)
                : 1;
            int poisonValue = context.IsEliteOrBossCombat ? 15 : 10;
            if (context.HasSustainedAttackPressure)
            {
                poisonValue += 3;
            }

            if (card.Type == CardType.Power)
            {
                poisonValue += context.IsEliteOrBossCombat ? 8 : 5;
            }

            score += poison * targetMultiplier * poisonValue;
        }

        return score;
    }

    private static int ScoreSelfBuff(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        AiCombatStatusWeights status = context.CombatConfig.Combat.StatusWeights;
        int temporaryStrength = card.GetSelfTemporaryStrengthAmount();
        int totalStrength = card.GetSelfStrengthAmount();
        int persistentStrength = Math.Max(0, totalStrength - temporaryStrength);
        int temporaryDexterity = card.GetSelfTemporaryDexterityAmount();
        int totalDexterity = card.GetSelfDexterityAmount();
        int persistentDexterity = Math.Max(0, totalDexterity - temporaryDexterity);

        int score = 0;
        if (temporaryStrength > 0)
        {
            score += temporaryStrength * Math.Max(
                status.TemporaryStrengthMinimumValue,
                CountAffordableAttackActions(context, action) * status.TemporaryStrengthPerAffordableAttackValue);
        }

        if (persistentStrength > 0)
        {
            score += persistentStrength * Math.Max(
                status.PersistentStrengthMinimumValue,
                CountAffordableAttackActions(context, action) * status.PersistentStrengthPerAffordableAttackValue);
        }

        if (temporaryDexterity > 0)
        {
            score += temporaryDexterity * Math.Max(
                status.TemporaryDexterityMinimumValue,
                CountAffordableBlockActions(context, action) * status.TemporaryDexterityPerAffordableBlockValue);
        }

        if (persistentDexterity > 0)
        {
            score += persistentDexterity * Math.Max(
                status.PersistentDexterityMinimumValue,
                CountAffordableBlockActions(context, action) * status.PersistentDexterityPerAffordableBlockValue);
        }

        if (card.Type == CardType.Power)
        {
            score += ScorePowerSetup(context, action, card);
        }

        return score;
    }

    private static int ScorePowerSetup(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        int uncoveredDamage = context.IncomingDamageAfterBlock;
        bool safeToSetup = uncoveredDamage <= 0 ||
                           context.CurrentHp - uncoveredDamage >= Math.Max(18, context.CurrentHp / 2);
        int score = 0;

        if (safeToSetup)
        {
            score += SafePowerSetupBonus;
        }
        else if (HasPlayableBlockAction(context))
        {
            score -= Math.Min(260, uncoveredDamage * 12);
        }
        else
        {
            score += Math.Min(75, Math.Max(0, context.CurrentHp - uncoveredDamage));
        }

        if (context.IsEliteOrBossCombat)
        {
            score += EliteBossPowerSetupBonus;
        }

        if (context.HasSustainedAttackPressure)
        {
            score += Math.Min(145, context.SustainedAttackPressure / 3);
        }

        if (!HasKnownUsefulCardEffect(card))
        {
            score += UnknownPowerSetupFloorBonus;
        }

        if (context.IsWaterfallSelfDestructDefenseWindow && uncoveredDamage > 0)
        {
            score -= 420;
        }

        if (CountAffordableUsefulNonPotionActions(context, action, extraEnergy: 0) <= 0 && !safeToSetup)
        {
            score -= 45;
        }

        return score;
    }

    private static int ScoreResourceSetup(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        AiCombatResourceWeights resource = context.CombatConfig.Combat.ResourceWeights;
        int cardsDrawn = card.GetCardsDrawn();
        int energyGain = card.GetEnergyGainWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        int score = 0;

        if (cardsDrawn > 0)
        {
            int knownBadDraws = StatusCardStrategy.EstimateKnownBadDraws(context, cardsDrawn);
            bool hasSpendableFollowUp = CountAffordableUsefulNonPotionActions(context, action, extraEnergy: energyGain) > 0;
            int usefulDraws = Math.Max(0, cardsDrawn - knownBadDraws);
            score += hasSpendableFollowUp
                ? usefulDraws * resource.DrawValueWhenPlayable
                : -usefulDraws * resource.DrawPenaltyWhenNotPlayable;
            if (knownBadDraws > 0)
            {
                int badDrawPenalty = StatusCardStrategy.IsLikelyHandCleanupCard(card)
                    ? resource.DrawPenaltyWhenNotPlayable
                    : resource.DrawPenaltyWhenNotPlayable + 28;
                score -= knownBadDraws * badDrawPenalty;
            }
        }

        if (energyGain > 0)
        {
            int unlockedFollowUps = CountEnergyUnlockedUsefulNonPotionActions(context, action, energyGain);
            if (unlockedFollowUps > 0 || cardsDrawn > 0)
            {
                score += energyGain * resource.EnergyGainValue;
                score += Math.Min(unlockedFollowUps, 3) * 12;
            }
            else
            {
                int consumablePenalty = card.Exhaust || card.Ethereal ? 55 : 20;
                score -= energyGain * resource.EnergyGainValue + consumablePenalty;
            }
        }

        return score;
    }

	    private static int ScoreStatusCleanup(DeterministicCombatContext context, ResolvedCardView card)
	    {
	        if (StatusCardStrategy.IsNegativeStatusOrCurse(card))
	        {
	            return ScorePlayableBurdenCard(context, card);
	        }

	        int score = 0;
	        int allowedTargetsInHand = StatusCardStrategy.CountAllowedHandCleanupTargets(context.HandCardsByInstanceId.Values, context.Actor);
	        if (StatusCardStrategy.IsLikelyHandCleanupCard(card))
	        {
	            if (allowedTargetsInHand > 0)
	            {
	                int cleanupTargets = Math.Min(allowedTargetsInHand, 3);
	                score += cleanupTargets * 58;
	                score += Math.Min(210, (int)Math.Round(StatusCardStrategy.SumAllowedCleanupBurden(context.HandCardsByInstanceId.Values, context.Actor) * 1.35d));
	                if (context.IncomingDamageAfterBlock > 0 || context.IsEliteOrBossCombat)
	                {
	                    score += cleanupTargets * 14;
	                }

	                if (StatusCardStrategy.IsUnsafeWholeHandCleanupCard(card))
	                {
	                    score -= context.IsEliteOrBossCombat ? 420 : 300;
	                }
            }
            else
            {
                score -= 5000;
            }
        }

        int cardsDrawn = card.GetCardsDrawn();
        int knownBadDraws = StatusCardStrategy.EstimateKnownBadDraws(context, cardsDrawn);
        if (knownBadDraws > 0)
        {
            score -= knownBadDraws * (StatusCardStrategy.IsLikelyHandCleanupCard(card) ? 18 : 54);
            if (knownBadDraws >= cardsDrawn && !StatusCardStrategy.IsLikelyHandCleanupCard(card))
            {
                score -= 45;
            }
        }

	        return score;
	    }

    private static int ScorePlayableBurdenCard(DeterministicCombatContext context, ResolvedCardView card)
    {
        if (StatusCardStrategy.IsBeckon(card))
        {
            int beckonScore = 105;
            if (context.IncomingDamageAfterBlock > 0)
            {
                beckonScore += Math.Min(70, context.IncomingDamageAfterBlock * 6);
            }

            if (context.CurrentHp <= Math.Max(18, context.IncomingDamageAfterBlock + 8))
            {
                beckonScore += 105;
            }

            if (CountAffordableNonPotionAttackActions(context) > 0)
            {
                beckonScore -= context.IsEliteOrBossCombat ? 145 : 85;
                if (context.HasSustainedAttackPressure)
                {
                    beckonScore -= 85;
                }
            }
            else
            {
                beckonScore += 50;
            }

            return beckonScore;
        }

        int score = 90 + (int)Math.Round(StatusCardStrategy.GetBurdenScore(card) * 3.2d);
        if (context.IncomingDamageAfterBlock > 0)
        {
            score += 40;
        }

        if (context.IsEliteOrBossCombat)
        {
            score += 55;
        }

        if (card.GetEstimatedDamage() > 0 &&
            StatusCardStrategy.HasAnyToken(card, "BURN", "DECAY") &&
            context.CurrentHp <= Math.Max(8, context.IncomingDamageAfterBlock + 4))
        {
            score -= 95;
        }

        return score;
    }

    private static int ScoreKillPotential(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        AiCombatRiskProfile risk = context.CombatConfig.Combat.RiskProfile;
        int estimatedDamage = card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
	        if (card.DealsDamageToAllEnemies())
	        {
	            int score = 0;
	            foreach (DeterministicEnemyState candidate in context.EnemiesById.Values)
	            {
	                int effectiveHp = candidate.CurrentHp + candidate.Block;
	                int effectiveDamage = EstimateEffectiveDamageAgainstEnemy(candidate, estimatedDamage, GetDamageHits(card));
	                if (effectiveDamage >= effectiveHp)
	                {
	                    score += risk.LethalPriorityBonus + candidate.IncomingDamage * risk.LethalIncomingDamageValue;
	                    if (candidate.HasSummonMove)
	                    {
	                        score += SummonIntentLethalBonus;
	                    }
	                }
	            }

            return score;
        }

        DeterministicEnemyState? enemy = null;
        if (!string.IsNullOrEmpty(action.TargetId))
        {
            context.EnemiesById.TryGetValue(action.TargetId, out enemy);
        }
        else if (context.EnemiesById.Count == 1)
        {
            enemy = context.EnemiesById.Values.First();
        }

        if (enemy == null)
        {
            return 0;
        }

        int effectiveEnemyHp = enemy.CurrentHp + enemy.Block;
	        int adjustedDamage = estimatedDamage;
	        if (enemy.HasVulnerable)
	        {
	            adjustedDamage += (int)Math.Ceiling(adjustedDamage * 0.5m);
	        }

	        int effectiveDamageToEnemy = EstimateEffectiveDamageAgainstEnemy(enemy, adjustedDamage, GetDamageHits(card));
	        if (effectiveDamageToEnemy >= effectiveEnemyHp)
	        {
	            int score = risk.LethalPriorityBonus + enemy.IncomingDamage * risk.LethalIncomingDamageValue;
	            if (enemy.HasSummonMove)
	            {
	                score += SummonIntentLethalBonus;
	            }

	            if (context.IsKaiserCrabCombat && enemy.IsKaiserCrabPart)
	            {
	                score += 520;
	            }

	            return score;
	        }

        return 0;
    }

    private static int ScoreTeamCoordination(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card)
    {
        DeterministicTeamCombatTactics tactics = context.TeamTactics;
        bool targetLock = (context.IsPhantasmalGardenersCombat || context.IsObscuraCombat || tactics.IsTargetLock) && tactics.HasPrimaryTarget;
        if (!tactics.HasFocusedKill && !targetLock)
        {
            return 0;
        }

        string primaryTargetId = tactics.PrimaryTargetId;
        int damageToPrimary = EstimateCardDamageToTarget(context, action, card, primaryTargetId);
        bool targetsPrimary = string.Equals(action.TargetId, primaryTargetId, StringComparison.Ordinal);
        bool damagesAnyEnemy = card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0 || card.Type == CardType.Attack;
        int targetBonus = context.IsObscuraCombat ? ObscuraTargetLockTargetBonus : targetLock ? GardenersTargetLockTargetBonus : TeamFocusedKillTargetBonus;
        int damageValue = context.IsObscuraCombat ? ObscuraTargetLockDamageValuePerPoint : targetLock ? GardenersTargetLockDamageValuePerPoint : TeamFocusedKillDamageValuePerPoint;
        int offTargetPenalty = context.IsObscuraCombat ? ObscuraTargetLockOffTargetPenalty : targetLock ? GardenersTargetLockOffTargetPenalty : TeamFocusedKillOffTargetPenalty;
        int nonDamagePenalty = context.IsObscuraCombat ? ObscuraTargetLockNonDamagePenalty : targetLock ? GardenersTargetLockNonDamagePenalty : TeamFocusedKillNonDamagePenalty;
        int score = 0;

        if (damageToPrimary > 0)
        {
            score += targetBonus;
            score += Math.Min(targetLock ? 360 : 260, damageToPrimary * damageValue);
            if (tactics.ActorCanContributeToPrimary)
            {
                score += Math.Min(90, tactics.EstimatedActorDamageToPrimary * 3);
            }

            if (context.EnemiesById.TryGetValue(primaryTargetId, out DeterministicEnemyState? primaryEnemy))
            {
                score += Math.Min(120, primaryEnemy.IncomingDamage * 5 + primaryEnemy.SustainedAttackPressure);
                if (targetLock && primaryEnemy.CurrentHp <= 30)
                {
                    int lowHpBonus = context.IsObscuraCombat ? ObscuraLowHpFocusBonus : GardenersLowHpFocusBonus;
                    score += Math.Max(35, lowHpBonus - primaryEnemy.CurrentHp * 3);
                }
            }
        }
        else if (IsOffTargetEnemyAction(context, action, card, primaryTargetId, damagesAnyEnemy))
        {
            score -= offTargetPenalty;
        }

        if (targetsPrimary && card.GetEnemyVulnerableAmount() > 0)
        {
            score += TeamFocusedKillDebuffBonus;
        }

        if (targetsPrimary && card.GetEnemyWeakAmount() > 0 &&
            context.EnemiesById.TryGetValue(primaryTargetId, out DeterministicEnemyState? weakTarget) &&
            weakTarget.IsAttacking)
        {
            score += TeamFocusedKillDebuffBonus / 2;
        }

        if (context.IsObscuraCombat &&
            targetLock &&
            damageToPrimary <= 0 &&
            !damagesAnyEnemy &&
            tactics.ActorCanContributeToPrimary &&
            CountAffordableNonPotionAttackActions(context) > 0 &&
            !IsGraveDanger(context))
        {
            score -= nonDamagePenalty;
        }

        bool pureDefense = (card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0 ||
                            card.GetSummonAmount() > 0) &&
                           card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) <= 0;
        if (pureDefense &&
            tactics.ActorCanContributeToPrimary &&
            !IsGraveDanger(context) &&
            context.IncomingDamageAfterBlock <= Math.Max(4, context.CurrentHp / 8))
        {
            score -= nonDamagePenalty;
        }

        return score;
    }

    private static int ScoreNonMinionLethalPriority(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card)
    {
        DeterministicTeamCombatTactics tactics = context.TeamTactics;
        if (!tactics.CanKillAllNonMinionEnemies ||
            tactics.NonMinionEnemyCount <= 0 ||
            !context.EnemiesById.Values.Any(static enemy => enemy.IsLikelySummonedAdd))
        {
            return 0;
        }

        bool damagesAnyEnemy = card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0 ||
                               card.Type == CardType.Attack;
        if (!damagesAnyEnemy)
        {
            if (IsGraveDanger(context))
            {
                return 0;
            }

            bool pureDefense = card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0 ||
                               card.GetSummonAmount() > 0;
            return pureDefense ? -NonMinionLethalNonDamagePenalty : 0;
        }

        int bestNonMinionDamage = 0;
        foreach (KeyValuePair<string, DeterministicEnemyState> enemy in context.EnemiesById.Where(static pair => !pair.Value.IsLikelySummonedAdd))
        {
            int damage = EstimateCardDamageToTarget(context, action, card, enemy.Key);
            bestNonMinionDamage = Math.Max(bestNonMinionDamage, Math.Min(damage, enemy.Value.CurrentHp + enemy.Value.Block));
        }

        if (bestNonMinionDamage > 0)
        {
            return NonMinionLethalTargetBonus + Math.Min(420, bestNonMinionDamage * NonMinionLethalDamageValuePerPoint);
        }

        if (!string.IsNullOrEmpty(action.TargetId) &&
            context.EnemiesById.TryGetValue(action.TargetId, out DeterministicEnemyState? target) &&
            target.IsLikelySummonedAdd)
        {
            return -NonMinionLethalMinionTargetPenalty;
        }

        return 0;
    }

    private static bool ShouldFocusCorpseSlugDebuffIntent(DeterministicCombatContext context)
    {
        int corpseSlugCount = context.EnemiesById.Values.Count(static enemy => enemy.IsCorpseSlug);
        return corpseSlugCount >= 2 &&
               context.EnemiesById.Values.Any(static enemy => enemy.IsCorpseSlugDebuffIntent) &&
               context.EnemiesById.Values.Any(static enemy => enemy.IsCorpseSlug && enemy.IsAttacking);
    }

    private static int ScoreTeamPotionCoordination(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        bool isDirectDamagePotion,
        bool isDebuffPotion,
        bool isImmediateBlockPotion,
        DeterministicPlayerState? playerTarget)
    {
        DeterministicTeamCombatTactics tactics = context.TeamTactics;
        bool targetLock = (context.IsPhantasmalGardenersCombat || context.IsObscuraCombat || tactics.IsTargetLock) && tactics.HasPrimaryTarget;
        if (!tactics.HasFocusedKill && !targetLock)
        {
            return 0;
        }

        string primaryTargetId = tactics.PrimaryTargetId;
        bool targetsPrimary = string.Equals(action.TargetId, primaryTargetId, StringComparison.Ordinal);
        int targetBonus = context.IsObscuraCombat ? ObscuraTargetLockTargetBonus : targetLock ? GardenersTargetLockTargetBonus : TeamFocusedKillTargetBonus;
        int damageValue = context.IsObscuraCombat ? ObscuraTargetLockDamageValuePerPoint : targetLock ? GardenersTargetLockDamageValuePerPoint : TeamFocusedKillDamageValuePerPoint;
        int offTargetPenalty = context.IsObscuraCombat ? ObscuraTargetLockOffTargetPenalty : targetLock ? GardenersTargetLockOffTargetPenalty : TeamFocusedKillOffTargetPenalty;
        int score = 0;
        int directPotionDamage = EstimateDirectPotionDamage(action);
        if (isDirectDamagePotion && (targetsPrimary || PotionDamagesAllEnemies(action)))
        {
            score += targetBonus;
            score += Math.Min(targetLock ? 320 : 220, directPotionDamage * damageValue);
        }
        else if (isDirectDamagePotion &&
                 !string.IsNullOrEmpty(action.TargetId) &&
                 context.EnemiesById.ContainsKey(action.TargetId))
        {
            score -= offTargetPenalty;
        }

        if (isDebuffPotion && targetsPrimary)
        {
            score += TeamFocusedKillDebuffBonus;
        }
        else if (isDebuffPotion &&
                 !string.IsNullOrEmpty(action.TargetId) &&
                 context.EnemiesById.ContainsKey(action.TargetId))
        {
            score -= offTargetPenalty / 2;
        }

        if (isImmediateBlockPotion &&
            tactics.ActorCanContributeToPrimary &&
            playerTarget?.IsInGraveDanger != true &&
            (playerTarget?.IncomingDamageAfterBlock ?? context.IncomingDamageAfterBlock) <= 0)
        {
            score -= TeamFocusedKillImmediateDefenseDrag;
        }

        return score;
    }

    private static int EstimateCardDamageToTarget(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card,
        string targetId)
    {
        OrbEvokeEstimate evoke = card.EstimateOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        int damage = card.GetEstimatedDamage() + evoke.Damage;
        if (damage <= 0)
        {
            return 0;
        }

        if (!card.DealsDamageToAllEnemies() &&
            context.EnemiesById.Count != 1 &&
            !string.Equals(action.TargetId, targetId, StringComparison.Ordinal))
        {
            return 0;
        }

        if (!context.EnemiesById.TryGetValue(targetId, out DeterministicEnemyState? enemy))
        {
            return damage;
        }

        int directHits = Math.Max(0, GetDamageHits(card));
        int adjustedDamage = damage + GetActorPowerAmount(context, "STRENGTH") * directHits;
        if (enemy.HasVulnerable)
        {
            adjustedDamage += (int)Math.Ceiling(adjustedDamage * 0.5m);
        }

        return Math.Max(0, adjustedDamage);
    }

    private static bool IsOffTargetEnemyAction(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView card,
        string primaryTargetId,
        bool damagesAnyEnemy)
    {
        if (string.IsNullOrEmpty(action.TargetId) ||
            string.Equals(action.TargetId, primaryTargetId, StringComparison.Ordinal) ||
            !context.EnemiesById.ContainsKey(action.TargetId))
        {
            return false;
        }

        return damagesAnyEnemy ||
               card.GetEnemyVulnerableAmount() > 0 ||
               card.GetEnemyWeakAmount() > 0;
    }

    private static bool PotionDamagesAllEnemies(AiLegalActionOption action)
    {
        return (action.CardId ?? string.Empty).Contains("EXPLOSIVE", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrEmpty(action.TargetId) ||
               string.Equals(action.TargetId, "none", StringComparison.Ordinal);
    }

    internal static bool IsLowConfidenceNoBenefitCard(ResolvedCardView? card)
    {
        if (card == null ||
            StatusCardStrategy.IsNegativeStatusOrCurse(card) ||
            HasKnownUsefulCardEffect(card) ||
            card.Type == CardType.Power)
        {
            return false;
        }

        return true;
    }

    internal static bool HasKnownUsefulCardEffect(ResolvedCardView? card)
    {
        return card.GetEstimatedDamage() > 0 ||
               card.GetEstimatedProtection() > 0 ||
               card.GetSummonAmount() > 0 ||
               card.GetCardsDrawn() > 0 ||
               card.GetEnergyGain() > 0 ||
               card.GetStarsGenerated() > 0 ||
               card.GetEnemyVulnerableAmount() > 0 ||
               card.GetEnemyWeakAmount() > 0 ||
               card.GetEnemyPoisonAmount() > 0 ||
               card.GetSelfStrengthAmount() > 0 ||
               card.GetSelfTemporaryStrengthAmount() > 0 ||
               card.GetSelfDexterityAmount() > 0 ||
               card.GetSelfTemporaryDexterityAmount() > 0 ||
               SpecialCardEffectHeuristics.HasKnownSpecialBenefit(card) ||
               card?.Type == CardType.Attack;
    }

    private static int ScoreLowConfidenceNoBenefitPenalty(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        if (card.EstimateOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0).HasAnyBenefit)
        {
            return 0;
        }

        if (!IsLowConfidenceNoBenefitCard(card))
        {
            return 0;
        }

        int penalty = LowConfidenceNoBenefitBasePenalty;
        if (context.IsEliteOrBossCombat)
        {
            penalty += 80;
        }

        if (context.HasSustainedAttackPressure)
        {
            penalty += Math.Min(140, context.SustainedAttackPressure / 2);
        }

        if (context.HasCatastrophicEnemyAction)
        {
            penalty += CatastrophicRaceNonDamagePenalty;
        }

        if (card.Exhaust || card.Ethereal)
        {
            penalty += 45;
        }

        return -penalty;
    }

    private static int ScoreUtility(DeterministicCombatContext context, AiLegalActionOption action)
    {
        AiCombatCoreWeights core = context.CombatConfig.Combat.CoreWeights;
        int uncoveredDamage = context.IncomingDamageAfterBlock;
        int score = uncoveredDamage > 0 ? core.UtilityValueWhenThreatened : core.UtilityValueWhenSafe;
        score += ScoreEnergyEfficiency(context, action);
        return score;
    }

    private static int ScorePotion(DeterministicCombatContext context, AiLegalActionOption action)
    {
        AiPotionCombatUseWeights potionUse = context.CombatConfig.Potions.CombatUse;
        bool isDebuffPotion = IsDebuffPotion(action);
        bool isDirectDamagePotion = IsDirectDamagePotion(action);
        bool isBuffPotion = IsBuffPotion(action);
        bool isResourcePotion = IsResourcePotion(action);
        bool isScalingPotion = IsScalingPotion(action);
        bool isHandFixPotion = IsHandFixPotion(action);
        bool isThornsPotion = IsThornsPotion(action);
        bool isDuplicationPotion = IsDuplicationPotion(action);
        bool isImmediateBlockPotion = IsImmediateBlockPotion(action);
        bool isPersistentDefensePotion = IsPersistentDefensePotion(action);
        bool isCleansingPotion = IsCleansingPotion(action);
        bool isTemporaryStrengthPotion = IsTemporaryStrengthPotion(action);
        bool isTemporaryDexterityPotion = IsTemporaryDexterityPotion(action);
        bool isPersistentScalingPotion = isScalingPotion || IsPersistentStrengthPotion(action);
        bool isOffensivePotion = isDebuffPotion || isDirectDamagePotion || IsAttackGeneratingPotion(action);
        bool isDefensivePotion = IsDefensivePotion(action) || isCleansingPotion;
        bool hasKnownCombatRole = isOffensivePotion ||
                                  isBuffPotion ||
                                  isResourcePotion ||
                                  isScalingPotion ||
                                  isHandFixPotion ||
                                  isThornsPotion ||
                                  isDuplicationPotion ||
                                  isDefensivePotion ||
                                  isImmediateBlockPotion ||
                                  isPersistentDefensePotion ||
                                  isCleansingPotion;
        bool graveDanger = IsGraveDanger(context);
        DeterministicPlayerState? playerTarget = ResolvePlayerTarget(context, action);
        bool targetGraveDanger = playerTarget?.IsInGraveDanger ?? graveDanger;
        int affordableAttackFollowUps = CountAffordableNonPotionAttackActions(context);
        int affordableBlockFollowUps = CountAffordableNonPotionBlockActions(context);
        bool canAmplifyAttacks = isDebuffPotion && affordableAttackFollowUps > 0;
        bool isSetupPotion = isDebuffPotion || isBuffPotion || isResourcePotion || isScalingPotion || isHandFixPotion || isThornsPotion || isDuplicationPotion || isPersistentDefensePotion || isCleansingPotion;
        bool isHighValueTarget = IsHighValuePotionTarget(context, action);
        bool isLongFight = IsLongFight(context);
        int targetIncomingAfterBlock = playerTarget?.IncomingDamageAfterBlock ?? context.IncomingDamageAfterBlock;
        int targetHp = Math.Max(1, playerTarget?.CurrentHp ?? context.CurrentHp);
        bool emergencyPotionWindow = context.IsTeamInCrisis ||
                                     graveDanger ||
                                     targetGraveDanger ||
                                     targetIncomingAfterBlock >= Math.Max(12, targetHp / 2) ||
                                     context.HasCatastrophicEnemyAction;

        int score = context.IsEliteOrBossCombat ? potionUse.EliteBossBaseScore : potionUse.NormalFightBaseScore;
        if (context.IsLagavulinMatriarchOpeningSetupWindow)
        {
            if (isDirectDamagePotion || IsAttackGeneratingPotion(action) || isThornsPotion)
            {
                score -= LagavulinSleepWakePenalty;
            }
            else if (isDebuffPotion || isScalingPotion || isBuffPotion || isResourcePotion || isHandFixPotion || isPersistentDefensePotion)
            {
                score += LagavulinSleepSetupBonus;
            }
        }

        if (!hasKnownCombatRole)
        {
            score -= UnclassifiedPotionUsePenalty;
        }

        if (context.PotionSlotsFull && !IsConservationPotion(action))
        {
            score += FullPotionSlotsUseBonus;
            if (context.IsEliteOrBossCombat)
            {
                score += FullPotionSlotsEliteBossBonus;
            }

            if (context.FuturePotionDropAfterCombat)
            {
                score += KnownFuturePotionDropFullSlotsUseBonus;
            }
        }

        if (context.IsEliteOrBossCombat)
        {
            score += potionUse.EliteBossBonus;
            if (!IsConservationPotion(action) && hasKnownCombatRole)
            {
                score += EliteBossUtilityPotionBonus;
            }
        }

        if (graveDanger || targetGraveDanger)
        {
            score += isDefensivePotion || isImmediateBlockPotion || isPersistentDefensePotion
                ? potionUse.GraveDangerDefensiveBonus
                : potionUse.GraveDangerOffensiveBonus;
        }

        if (!IsConservationPotion(action) &&
            emergencyPotionWindow)
        {
            score += hasKnownCombatRole ? KnownPotionTeamCrisisUseBonus : UnknownPotionEmergencyUseBonus;
            if (targetIncomingAfterBlock >= Math.Max(12, targetHp / 2))
            {
                score += SevereIncomingPotionUseBonus;
            }

            if (context.IsEliteOrBossCombat)
            {
                score += 45;
            }
        }

        score += ScoreFriendlyPotionTargeting(context, action, playerTarget, isImmediateBlockPotion, isPersistentDefensePotion, isCleansingPotion, isBuffPotion, isResourcePotion, isHandFixPotion, isDuplicationPotion, isOffensivePotion);

        if (isScalingPotion && isLongFight)
        {
            score += LongFightScalingPotionBonus + Math.Min(80, TotalEnemyEffectiveHp(context) / 4);
        }

        if (!context.IsEliteOrBossCombat &&
            !IsConservationPotion(action) &&
            !emergencyPotionWindow &&
            !context.PotionSlotsFull &&
            !context.FuturePotionDropAfterCombat &&
            !WouldPotionLethalEnemy(context, action))
        {
            score -= NormalFightPotionConservationPenalty;
            if (isPersistentScalingPotion)
            {
                score -= NormalFightScalingPotionConservationPenalty;
            }
        }

        if (isHandFixPotion && (context.IsEliteOrBossCombat || graveDanger || HasPoorHand(context)))
        {
            score += HighPressureHandFixPotionBonus;
        }

        if (isThornsPotion && context.IncomingDamage > 0)
        {
            score += ThornsPotionUnderAttackBonus + Math.Min(80, context.IncomingDamage * 2);
        }

        if (isDuplicationPotion && affordableAttackFollowUps + affordableBlockFollowUps > 0)
        {
            score += DuplicationPotionFollowUpBonus;
        }

        if (isOffensivePotion)
        {
            if (context.IsEliteOrBossCombat && canAmplifyAttacks)
            {
                score += potionUse.EliteBossOffensiveFollowUpBonus;
            }
            else if (!context.IsEliteOrBossCombat && canAmplifyAttacks && isHighValueTarget)
            {
                score += potionUse.NormalFightOffensiveFollowUpBonus;
            }
        }

        if (isSetupPotion)
        {
            bool hasRelevantFollowUp =
                                       (isDebuffPotion && affordableAttackFollowUps > 0) ||
                                       (isBuffPotion && !isTemporaryStrengthPotion && !isTemporaryDexterityPotion && affordableAttackFollowUps + affordableBlockFollowUps > 0) ||
                                       (isTemporaryStrengthPotion && affordableAttackFollowUps > 0) ||
                                       (isTemporaryDexterityPotion && affordableBlockFollowUps > 0 && context.TeamIncomingDamageAfterBlock > 0) ||
                                       (isDefensivePotion && !isCleansingPotion && !isTemporaryDexterityPotion && affordableBlockFollowUps > 0) ||
                                       (isResourcePotion && CountPlayableNonPotionActions(context) > 0) ||
                                       (isScalingPotion && isLongFight) ||
                                       (isHandFixPotion && (HasPoorHand(context) || context.IsEliteOrBossCombat)) ||
                                       (isThornsPotion && context.IncomingDamage > 0) ||
                                       (isDuplicationPotion && affordableAttackFollowUps + affordableBlockFollowUps > 0) ||
                                       (isCleansingPotion && IsCleansingPotionUseful(context, playerTarget)) ||
                                       (isPersistentDefensePotion && IsPersistentDefensePotionUseful(context, playerTarget));
            score += hasRelevantFollowUp ? SetupPotionWithFollowUpBonus : -SetupPotionWithoutFollowUpPenalty;
        }

        if (isTemporaryStrengthPotion && affordableAttackFollowUps <= 0)
        {
            score -= MeaninglessTemporaryPotionUsePenalty;
        }

        if (isTemporaryDexterityPotion &&
            (affordableBlockFollowUps <= 0 || context.TeamIncomingDamageAfterBlock <= 0))
        {
            score -= MeaninglessTemporaryPotionUsePenalty;
        }

        if (!string.IsNullOrEmpty(action.TargetId) &&
            context.EnemiesById.TryGetValue(action.TargetId, out DeterministicEnemyState? enemy))
        {
            if (enemy.IsAttacking)
            {
                score += potionUse.AttackingTargetBonus;
            }

            if (enemy.SustainedAttackPressure > 0 && isOffensivePotion)
            {
                score += Math.Min(170, enemy.SustainedAttackPressure + (canAmplifyAttacks ? 45 : 0));
            }

            int directPotionDamage = EstimateDirectPotionDamage(action);
            if (isDirectDamagePotion && directPotionDamage >= enemy.CurrentHp + enemy.Block)
            {
                score += DirectPotionLethalBonus + enemy.IncomingDamage;
            }

            if (isDebuffPotion && enemy.HasArtifact)
            {
                score -= 30;
            }

            if (isDebuffPotion &&
                ((IsVulnerablePotion(action) && enemy.HasVulnerable) ||
                 (IsWeakPotion(action) && enemy.HasWeak)))
            {
                score -= 28;
            }

            if (!isDirectDamagePotion && enemy.CurrentHp + enemy.Block <= 18)
            {
                score -= potionUse.LowHealthTargetPenalty;
            }
        }

        int teamCoordinationScore = ScoreTeamPotionCoordination(context, action, isDirectDamagePotion, isDebuffPotion, isImmediateBlockPotion, playerTarget);
        score += teamCoordinationScore;

        Log.Debug(
            $"[AITeammate] Potion score actionId={action.ActionId} potion={action.CardId ?? "unknown"} full={context.PotionSlotsFull} futureDrop={context.FuturePotionDropAfterCombat} futureDropInfo={context.FuturePotionDropDescription} teamCrisis={context.IsTeamInCrisis} alive={context.AlivePlayerCount} teamIncoming={context.TeamIncomingDamageAfterBlock} targetIncoming={playerTarget?.IncomingDamageAfterBlock.ToString() ?? "n/a"} targetHp={playerTarget?.CurrentHp.ToString() ?? "n/a"} atkFollow={affordableAttackFollowUps} blockFollow={affordableBlockFollowUps} emergency={emergencyPotionWindow} team={teamCoordinationScore} score={score}");
        return score;
    }

    private static int ScoreEndTurn(DeterministicCombatContext context)
    {
        AiCombatResourceWeights resource = context.CombatConfig.Combat.ResourceWeights;
        if (context.IsLagavulinMatriarchOpeningSetupWindow)
        {
            return 45;
        }

        if (ShouldPreferEndTurnOverRemainingPotions(context))
        {
            return resource.EndTurnWhenSkippingPotionsBonus;
        }

        return context.LegalActions.Count > 1 ? -resource.EndTurnWhileOtherActionsExistPenalty : 0;
    }

    private static int ScoreEnergyEfficiency(DeterministicCombatContext context, AiLegalActionOption action)
    {
        if (!action.EnergyCost.HasValue)
        {
            return 0;
        }

        return Math.Max(0, 4 - action.EnergyCost.Value) * context.CombatConfig.Combat.ResourceWeights.EnergyEfficiencyValue;
    }

    private static int GetActorPowerAmount(DeterministicCombatContext context, string powerId)
    {
        return context.ActorPowerAmounts.TryGetValue(powerId, out int amount) ? amount : 0;
    }

    private static int GetDamageHits(ResolvedCardView card)
    {
        return card.GetDirectDamageHits();
    }

    private static int EstimateWeakPrevention(DeterministicCombatContext context, AiLegalActionOption action, int weakAmount)
    {
        if (weakAmount <= 0)
        {
            return 0;
        }

        if (!string.IsNullOrEmpty(action.TargetId) &&
            context.EnemiesById.TryGetValue(action.TargetId, out DeterministicEnemyState? enemy))
        {
            return Math.Max(1, enemy.IncomingDamage / 4);
        }

        return Math.Max(1, context.IncomingDamage / 6);
    }

    private static int EstimateAllEnemyWeakPrevention(DeterministicCombatContext context, int weakAmount)
    {
        if (weakAmount <= 0)
        {
            return 0;
        }

        int total = context.EnemiesById.Values.Sum(static enemy => Math.Max(1, enemy.IncomingDamage / 4));
        return Math.Max(total, Math.Max(1, context.IncomingDamage / 6));
    }

    private static bool HasPlayableBlockAction(DeterministicCombatContext context)
    {
        foreach (AiLegalActionOption action in context.LegalActions)
        {
            ResolvedCardView? card = ResolveCard(context, action);
            if (card?.HasEffect(EffectKind.GainBlock) == true ||
                card?.HasEffect(EffectKind.Summon) == true ||
                card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAffordableBlockFollowUp(DeterministicCombatContext context, AiLegalActionOption currentAction)
    {
        return CountAffordableBlockActions(context, currentAction) > 0;
    }

    private static int CountAffordableAttackActions(DeterministicCombatContext context, AiLegalActionOption currentAction)
    {
        return context.LegalActions.Count(candidate =>
        {
            if (string.Equals(candidate.ActionId, currentAction.ActionId, StringComparison.Ordinal))
            {
                return false;
            }

            if ((candidate.EnergyCost ?? 0) > Math.Max(0, context.Energy - (currentAction.EnergyCost ?? 0)))
            {
                return false;
            }

            ResolvedCardView? card = ResolveCard(context, candidate);
            return card?.HasEffect(EffectKind.DealDamage) == true ||
                   card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
                   card?.Type == CardType.Attack;
        });
    }

    private static int CountAffordableBlockActions(DeterministicCombatContext context, AiLegalActionOption currentAction)
    {
        return context.LegalActions.Count(candidate =>
        {
            if (string.Equals(candidate.ActionId, currentAction.ActionId, StringComparison.Ordinal))
            {
                return false;
            }

            if ((candidate.EnergyCost ?? 0) > Math.Max(0, context.Energy - (currentAction.EnergyCost ?? 0)))
            {
                return false;
            }

            ResolvedCardView? card = ResolveCard(context, candidate);
            return card?.HasEffect(EffectKind.GainBlock) == true ||
                   card?.HasEffect(EffectKind.Summon) == true ||
                   card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0;
        });
    }

    private static int CountAffordableUsefulNonPotionActions(DeterministicCombatContext context, AiLegalActionOption currentAction, int extraEnergy)
    {
        int remainingEnergy = Math.Max(0, context.Energy - (currentAction.EnergyCost ?? 0) + extraEnergy);
        return context.LegalActions.Count(candidate =>
            !string.Equals(candidate.ActionId, currentAction.ActionId, StringComparison.Ordinal) &&
            !string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) &&
            !string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) &&
            (candidate.EnergyCost ?? 0) <= remainingEnergy &&
            IsUsefulNonPotionFollowUp(context, candidate));
    }

    private static int CountEnergyUnlockedUsefulNonPotionActions(DeterministicCombatContext context, AiLegalActionOption currentAction, int extraEnergy)
    {
        int energyWithoutGain = Math.Max(0, context.Energy - (currentAction.EnergyCost ?? 0));
        int energyWithGain = energyWithoutGain + Math.Max(0, extraEnergy);
        return context.LegalActions.Count(candidate =>
            !string.Equals(candidate.ActionId, currentAction.ActionId, StringComparison.Ordinal) &&
            !string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) &&
            !string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) &&
            (candidate.EnergyCost ?? 0) > energyWithoutGain &&
            (candidate.EnergyCost ?? 0) <= energyWithGain &&
            IsUsefulNonPotionFollowUp(context, candidate));
    }

    private static bool IsUsefulNonPotionFollowUp(DeterministicCombatContext context, AiLegalActionOption candidate)
    {
        ResolvedCardView? card = ResolveCard(context, candidate);
        if (card == null)
        {
            return false;
        }

        return card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
               card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
               card.GetSummonAmount() > 0 ||
               card.GetCardsDrawn() > 0 ||
               card.GetEnergyGainWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
               card.GetEnemyVulnerableAmount() > 0 ||
               card.GetEnemyWeakAmount() > 0 ||
               card.GetSelfStrengthAmount() > 0 ||
               card.GetSelfDexterityAmount() > 0 ||
               card.Type == CardType.Attack ||
               card.Type == CardType.Power;
    }

    private static int CountNonPotionAttackActions(DeterministicCombatContext context)
    {
        int count = 0;
        foreach (AiLegalActionOption candidate in context.LegalActions)
        {
            if (string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            ResolvedCardView? card = ResolveCard(context, candidate);
            if (card?.HasEffect(EffectKind.DealDamage) == true ||
                card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
                card?.Type == CardType.Attack)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountAffordableNonPotionAttackActions(DeterministicCombatContext context)
    {
        return context.LegalActions.Count(candidate =>
        {
            if (string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                (candidate.EnergyCost ?? 0) > context.Energy)
            {
                return false;
            }

            ResolvedCardView? card = ResolveCard(context, candidate);
            return card?.HasEffect(EffectKind.DealDamage) == true ||
                   card.GetEstimatedDamageWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0 ||
                   card?.Type == CardType.Attack;
        });
    }

    private static int CountAffordableNonPotionBlockActions(DeterministicCombatContext context)
    {
        return context.LegalActions.Count(candidate =>
        {
            if (string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                (candidate.EnergyCost ?? 0) > context.Energy)
            {
                return false;
            }

            ResolvedCardView? card = ResolveCard(context, candidate);
            return card?.HasEffect(EffectKind.GainBlock) == true ||
                   card?.HasEffect(EffectKind.Summon) == true ||
                   card.GetEstimatedBlockWithOrbEvoke(context.Actor, context.Energy, candidate.EnergyCost ?? 0) > 0;
        });
    }

    private static int CountPlayableNonPotionActions(DeterministicCombatContext context)
    {
        return context.LegalActions.Count(candidate =>
            !string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) &&
            !string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) &&
            (candidate.EnergyCost ?? 0) <= context.Energy);
    }

    private static bool IsOffensivePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BINDING", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("VULNERABLE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("WEAK", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("POISON", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DOOM", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FIRE", StringComparison.OrdinalIgnoreCase)
               || IsAttackGeneratingPotion(action);
    }

    private static bool IsDebuffPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BINDING", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("VULNERABLE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("WEAK", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("POISON", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DOOM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectDamagePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("FIRE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("EXPLOSIVE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("AMPOULE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuffPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("STRENGTH", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FLEX", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DEXTERITY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("SPEED", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FYSH_OIL", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FISH_OIL", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FOCUS", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("CAPACITY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("CULTIST", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("STABLE_SERUM", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("BLESSING_OF_THE_FORGE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsResourcePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("ENERGY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DRAW", StringComparison.OrdinalIgnoreCase)
               || IsHandFixPotion(action);
    }

    private static bool IsVulnerablePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("VULNERABLE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("BINDING", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeakPotion(AiLegalActionOption action)
    {
        return (action.CardId ?? string.Empty).Contains("WEAK", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConservationPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("FAIRY", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("BOTTLE", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("REVIVE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScalingPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("FOCUS", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("CAPACITY", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("CULTIST", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPersistentStrengthPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("STRENGTH", StringComparison.OrdinalIgnoreCase) &&
               !potionId.Contains("FLEX", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryStrengthPotion(AiLegalActionOption action)
    {
        return (action.CardId ?? string.Empty).Contains("FLEX", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryDexterityPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("SPEED", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("FYSH_OIL", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("FISH_OIL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHandFixPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("GAMBL", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("SWIFT", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("SKILL_POTION", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("ATTACK_POTION", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("POWER_POTION", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("COLORLESS", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("LIQUID_MEMORIES", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("ENTROPIC", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("STABLE_SERUM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThornsPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("LIQUID_BRONZE", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("BRONZE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDuplicationPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("DUPLICATOR", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("DUPLICATION", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAttackGeneratingPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("ATTACK_POTION", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimateDirectPotionDamage(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        if (potionId.Contains("FIRE", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        if (potionId.Contains("EXPLOSIVE", StringComparison.OrdinalIgnoreCase) ||
            potionId.Contains("AMPOULE", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return 0;
    }

    private static bool WouldPotionLethalEnemy(DeterministicCombatContext context, AiLegalActionOption action)
    {
        int damage = EstimateDirectPotionDamage(action);
        if (damage <= 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(action.TargetId) &&
            context.EnemiesById.TryGetValue(action.TargetId, out DeterministicEnemyState? target))
        {
            return damage >= target.CurrentHp + target.Block;
        }

        if (!PotionDamagesAllEnemies(action))
        {
            return false;
        }

        return context.EnemiesById.Values.Any(enemy => damage >= enemy.CurrentHp + enemy.Block);
    }

    private static bool IsDefensivePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BLOCK", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("ARMOR", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("HEART_OF_IRON", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DEXTERITY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("SPEED", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FYSH_OIL", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FISH_OIL", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("CURE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("WEAK", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("REGEN", StringComparison.OrdinalIgnoreCase)
               || IsThornsPotion(action);
    }

    private static bool IsImmediateBlockPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BLOCK", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("ARMOR", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimateImmediateBlockPotionAmount(AiLegalActionOption action)
    {
        if (!IsImmediateBlockPotion(action))
        {
            return 0;
        }

        string potionId = action.CardId ?? string.Empty;
        if (potionId.Contains("BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            return 12;
        }

        return 10;
    }

    private static bool IsPersistentDefensePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("HEART_OF_IRON", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("METALLIC", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("REGEN", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCleansingPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("CURE", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("CLEANSE", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("PURIFY", StringComparison.OrdinalIgnoreCase);
    }

    private static DeterministicPlayerState? ResolvePlayerTarget(DeterministicCombatContext context, AiLegalActionOption action)
    {
        if (!string.IsNullOrEmpty(action.TargetId) &&
            context.PlayerStatesById.TryGetValue(action.TargetId, out DeterministicPlayerState? target))
        {
            return target;
        }

        if (!string.IsNullOrEmpty(action.TargetId) &&
            !string.Equals(action.TargetId, "none", StringComparison.Ordinal))
        {
            return null;
        }

        string actorTargetId = $"player_{context.Actor.NetId}";
        return context.PlayerStatesById.TryGetValue(actorTargetId, out DeterministicPlayerState? actorTarget)
            ? actorTarget
            : null;
    }

    private static int ScoreFriendlyPotionTargeting(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        DeterministicPlayerState? target,
        bool isImmediateBlockPotion,
        bool isPersistentDefensePotion,
        bool isCleansingPotion,
        bool isBuffPotion,
        bool isResourcePotion,
        bool isHandFixPotion,
        bool isDuplicationPotion,
        bool isOffensivePotion)
    {
        if (target == null)
        {
            return 0;
        }

        int score = 0;
        if (isImmediateBlockPotion)
        {
            int blockAmount = EstimateImmediateBlockPotionAmount(action);
            int preventedDamage = Math.Min(blockAmount, target.IncomingDamageAfterBlock);
            if (preventedDamage <= 0)
            {
                return -ImmediateDefenseNoThreatPenalty;
            }

            score += preventedDamage * ImmediateDefensePreventedDamageValue;
            score += Math.Min(70, target.IncomingDamageAfterBlock * 4);
            if (target.IsInGraveDanger)
            {
                score += ImmediateDefenseCriticalTargetBonus;
            }

            if (!target.IsActor)
            {
                score += Math.Min(35, target.MissingHp);
            }

            return score;
        }

        if (isPersistentDefensePotion)
        {
            score += ScorePersistentDefensePotionTarget(context, target);
        }

        if (isCleansingPotion)
        {
            score += ScoreCleansingPotionTarget(context, target, target.IncomingDamageAfterBlock);
        }

        bool selfTempoPotion = isBuffPotion ||
                               isResourcePotion ||
                               isHandFixPotion ||
                               isDuplicationPotion ||
                               IsAttackGeneratingPotion(action) ||
                               (isOffensivePotion && !isPersistentDefensePotion);
        if (selfTempoPotion)
        {
            if (target.IsActor)
            {
                score += FriendlyTempoSelfTargetBonus;
            }
            else
            {
                score -= FriendlyTempoWrongTargetPenalty;
            }
        }

        return score;
    }

    private static int ScorePersistentDefensePotionTarget(DeterministicCombatContext context, DeterministicPlayerState target)
    {
        int score = 0;
        if (!IsPersistentDefensePotionUseful(context, target))
        {
            score -= PersistentDefenseNoNeedPenalty;
        }

        if (target.IncomingDamageAfterBlock > 0)
        {
            score += Math.Min(95, target.IncomingDamageAfterBlock * 7);
        }

        if (target.IsInGraveDanger)
        {
            score += 90;
        }

        score += Math.Min(45, target.MissingHp / 2);
        score += target.IsActor ? 14 : -20;
        return score;
    }

    private static int ScoreCleansingPotionTarget(
        DeterministicCombatContext context,
        DeterministicPlayerState? target,
        int targetIncomingAfterBlock)
    {
        if (target == null)
        {
            return context.IsTeamInCrisis ? 45 : -60;
        }

        int score = 0;
        if (targetIncomingAfterBlock > 0)
        {
            score += Math.Min(85, targetIncomingAfterBlock * 5);
        }

        if (target.IsInGraveDanger)
        {
            score += 95;
        }

        if (target.MissingHp >= Math.Max(10, target.MaxHp / 5))
        {
            score += Math.Min(120, target.MissingHp * 4);
        }

        if (context.IsEliteOrBossCombat && target.MissingHp >= Math.Max(8, target.MaxHp / 6))
        {
            score += 55;
        }

        if (!target.IsActor && target.MissingHp > 0)
        {
            score += Math.Min(50, target.MissingHp * 2);
        }

        return score > 0 ? score : -70;
    }

    private static bool IsCleansingPotionUseful(DeterministicCombatContext context, DeterministicPlayerState? target)
    {
        if (target == null)
        {
            return context.IsTeamInCrisis || context.IsEliteOrBossCombat;
        }

        return target.IsInGraveDanger ||
               target.IncomingDamageAfterBlock > 0 ||
               target.MissingHp >= Math.Max(10, target.MaxHp / 5) ||
               context.IsTeamInCrisis;
    }

    private static bool IsPersistentDefensePotionUseful(DeterministicCombatContext context, DeterministicPlayerState? target)
    {
        if (target == null)
        {
            return context.IsEliteOrBossCombat || context.HasSustainedAttackPressure || context.IncomingDamageAfterBlock > 0;
        }

        return context.IsEliteOrBossCombat ||
               context.HasSustainedAttackPressure ||
               target.IncomingDamageAfterBlock > 0 ||
               target.MissingHp >= Math.Max(12, target.MaxHp / 4);
    }

    private static bool IsGraveDanger(DeterministicCombatContext context)
    {
        int uncoveredDamage = context.IncomingDamageAfterBlock;
        return uncoveredDamage >= Math.Max(10, context.CurrentHp / 3) || uncoveredDamage >= context.CurrentHp;
    }

    private static bool IsLongFight(DeterministicCombatContext context)
    {
        return context.IsEliteOrBossCombat ||
               context.HasSustainedAttackPressure ||
               TotalEnemyEffectiveHp(context) >= 70;
    }

    private static bool HasPoorHand(DeterministicCombatContext context)
    {
        if (StatusCardStrategy.CountNegativeStatusOrCurse(context.HandCardsByInstanceId.Values) >= 2)
        {
            return true;
        }

        int playableNonPotion = CountPlayableNonPotionActions(context);
        if (playableNonPotion <= 0)
        {
            return true;
        }

        bool hasAttack = CountAffordableNonPotionAttackActions(context) > 0;
        bool hasBlock = CountAffordableNonPotionBlockActions(context) > 0;
        if (context.IncomingDamageAfterBlock > 0 && !hasBlock)
        {
            return true;
        }

        return TotalEnemyEffectiveHp(context) > 0 && !hasAttack && !hasBlock;
    }

    private static int TotalEnemyEffectiveHp(DeterministicCombatContext context)
    {
        return context.EnemiesById.Values.Sum(static enemy => Math.Max(0, enemy.CurrentHp + enemy.Block));
    }

    private static bool IsHighValuePotionTarget(DeterministicCombatContext context, AiLegalActionOption action)
    {
        if (string.IsNullOrEmpty(action.TargetId) ||
            !context.EnemiesById.TryGetValue(action.TargetId, out DeterministicEnemyState? enemy))
        {
            return false;
        }

        return enemy.IsAttacking || enemy.SustainedAttackPressure > 0 || enemy.CurrentHp + enemy.Block >= 24;
    }

    internal static int EstimateSustainedAttackRaceHpReserve(DeterministicCombatContext context)
    {
        int maxHp = Math.Max(context.Actor.Creature.MaxHp, context.CurrentHp);
        return Math.Max(10, Math.Max(maxHp / 4, context.IncomingDamage / 2));
    }

    private static bool ShouldPreferEndTurnOverRemainingPotions(DeterministicCombatContext context)
    {
        return context.LegalActions
            .Where(action => string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
            .All(action => ScorePotion(context, action) <= 0);
    }
}
