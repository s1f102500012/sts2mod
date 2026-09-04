using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class CardChoiceEvaluator
{
    private readonly CardEvaluationContextFactory _contextFactory = new();

    public CardEvaluationContextFactory ContextFactory => _contextFactory;

    public CardChoiceDecision EvaluateCandidates(
        IEnumerable<CardModel> candidates,
        CardEvaluationContext context)
    {
        AiCardRewardTuning tuning = AiCharacterCombatConfigLoader.LoadForPlayer(context.Player).CardRewards;
        List<CardEvaluationResult> ranked = candidates
            .Select((card, index) => EvaluateCard(card, index, context))
            .OrderByDescending(static result => result.FinalScore)
            .ThenBy(result => result.Candidate.Name, StringComparer.Ordinal)
            .ToList();

        double skipThreshold = GetSkipThreshold(context, tuning);
        bool shouldTake = ranked.Count > 0 && (!context.SkipAllowed || ranked[0].FinalScore >= skipThreshold);

        return new CardChoiceDecision
        {
            RankedResults = ranked,
            SkipThreshold = skipThreshold,
            ShouldTakeCard = shouldTake
        };
    }

    private CardEvaluationResult EvaluateCard(CardModel cardModel, int index, CardEvaluationContext context)
    {
        AiCardRewardTuning tuning = AiCharacterCombatConfigLoader.LoadForPlayer(context.Player).CardRewards;
        ResolvedCardView card = _contextFactory.ResolveCandidate(cardModel, index);
        CardFeatureVector features = CardFeatureVector.From(card);

        double intrinsic = ScoreIntrinsic(card, features, tuning);
        double deckFit = ScoreDeckFit(card, features, context, tuning);
        double needs = ScoreDeckNeeds(card, features, context, tuning);
        double redundancy = ScoreRedundancy(card, features, context, tuning);
        double priorityPlan = CardRewardPriorityPlanner.ScoreRewardCard(card, context);
        double guideStrategy = GuideCardRewardPlanner.ScoreRewardCard(card, context);
        double archetypeStrategy = CharacterArchetypePlanner.ScoreRewardCard(card, context);
        double ironcladStrategy = IroncladCharacterStrategy.ScoreRewardCard(card, context);
        double silentStrategy = SilentCharacterStrategy.ScoreRewardCard(card, context);
        double defectStrategy = DefectCharacterStrategy.ScoreRewardCard(card, context);
        double necrobinderStrategy = NecrobinderCharacterStrategy.ScoreRewardCard(card, context);
        double contextAdjustment = ScoreContext(card, features, context, tuning) +
                                   ScoreRunPhaseStrategy(card, features, context) +
                                   ScoreFutureRewardAwareness(card, context) +
                                   priorityPlan +
                                   guideStrategy +
                                   archetypeStrategy +
                                   ironcladStrategy +
                                   silentStrategy +
                                   defectStrategy +
                                   necrobinderStrategy +
                                   RegentCharacterStrategy.ScoreRewardCard(card, context);
        double final = intrinsic + deckFit + needs + contextAdjustment - redundancy;

        List<string> reasons = [];
        if (intrinsic > 0)
        {
            reasons.Add($"intrinsic +{intrinsic:F1}");
        }

        if (deckFit > 0)
        {
            reasons.Add($"fit +{deckFit:F1}");
        }

        if (needs > 0)
        {
            reasons.Add($"needs +{needs:F1}");
        }

        if (redundancy > 0)
        {
            reasons.Add($"redundancy -{redundancy:F1}");
        }

        if (contextAdjustment != 0)
        {
            reasons.Add($"context {(contextAdjustment > 0 ? "+" : string.Empty)}{contextAdjustment:F1}");
        }

        if (priorityPlan != 0)
        {
            reasons.Add($"priorityPlan {(priorityPlan > 0 ? "+" : string.Empty)}{priorityPlan:F1}");
        }

        if (guideStrategy != 0)
        {
            reasons.Add($"guideStrategy {(guideStrategy > 0 ? "+" : string.Empty)}{guideStrategy:F1}");
        }

        if (archetypeStrategy != 0)
        {
            reasons.Add($"archetype {(archetypeStrategy > 0 ? "+" : string.Empty)}{archetypeStrategy:F1}");
        }

        if (ironcladStrategy != 0)
        {
            reasons.Add($"ironcladAct1 {(ironcladStrategy > 0 ? "+" : string.Empty)}{ironcladStrategy:F1}");
        }

        if (silentStrategy != 0)
        {
            reasons.Add($"silentAct1 {(silentStrategy > 0 ? "+" : string.Empty)}{silentStrategy:F1}");
        }

        if (defectStrategy != 0)
        {
            reasons.Add($"defectAct1 {(defectStrategy > 0 ? "+" : string.Empty)}{defectStrategy:F1}");
        }

        if (necrobinderStrategy != 0)
        {
            reasons.Add($"necrobinderAct1 {(necrobinderStrategy > 0 ? "+" : string.Empty)}{necrobinderStrategy:F1}");
        }

        return new CardEvaluationResult
        {
            CandidateCard = cardModel,
            Candidate = card,
            FinalScore = final,
            IntrinsicScore = intrinsic,
            DeckFitScore = deckFit,
            NeedCoverageScore = needs,
            RedundancyPenalty = redundancy,
            ContextAdjustmentScore = contextAdjustment,
            Reasons = reasons
        };
    }

    private static double ScoreIntrinsic(ResolvedCardView card, CardFeatureVector features, AiCardRewardTuning tuning)
    {
        AiCardRewardIntrinsicWeights intrinsic = tuning.IntrinsicWeights;
        double score = 0d;
        score += features.Damage * intrinsic.DamageValuePerPoint;
        score += features.Block * intrinsic.BlockValuePerPoint;
        score += features.Summon * intrinsic.BlockValuePerPoint * 1.25d;
        score += features.Draw * intrinsic.DrawValue;
        score += features.Energy * intrinsic.EnergyValue;
        score += features.Vulnerable * intrinsic.VulnerableValue;
        score += features.Weak * intrinsic.WeakValue;
        score += features.Poison * 2.6d;
        score += features.PersistentStrength * intrinsic.PersistentStrengthValue;
        score += features.PersistentDexterity * intrinsic.PersistentDexterityValue;
        score += features.TemporaryStrength * intrinsic.TemporaryStrengthValue;
        score += features.TemporaryDexterity * intrinsic.TemporaryDexterityValue;
        score += features.SpecialUtility * 0.8d;
        score += features.RepeatCount * intrinsic.RepeatValue;
        score += GetRarityBonus(card.Rarity, intrinsic);

        if (card.Type == CardType.Power)
        {
            score += intrinsic.PowerBonus;
        }

        if (card.EffectiveCost == 0)
        {
            score += intrinsic.ZeroCostBonus;
        }
        else if (card.EffectiveCost > 1)
        {
            score -= (card.EffectiveCost - 1) * intrinsic.HighCostPenaltyPerExtraEnergy;
        }

        if (card.Retain)
        {
            score += intrinsic.RetainBonus;
        }

        if (card.Exhaust)
        {
            score += score >= 18d ? intrinsic.GoodExhaustBonus : -intrinsic.BadExhaustPenalty;
        }

        if (card.Ethereal)
        {
            score -= intrinsic.EtherealPenalty;
        }

        if (features.TotalKnownValue <= 0 &&
            card.Type is CardType.Attack or CardType.Skill or CardType.Power)
        {
            score -= intrinsic.UnknownValuePenalty;
        }

        return score;
    }

    private static double ScoreDeckFit(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context, AiCardRewardTuning tuning)
    {
        AiCardRewardSynergyWeights synergy = tuning.SynergyWeights;
        DeckSummary deck = context.DeckSummary;
        double score = 0d;

        if (features.Draw > 0 && (deck.HighCostCards >= 4 || deck.AverageCost >= 1.35d))
        {
            score += features.Draw * synergy.DrawWithHighCurveValue;
        }

        if (features.Energy > 0 && (deck.HighCostCards >= 5 || deck.DrawSources >= 2))
        {
            double energyFitMultiplier = NeedsMoreEnergy(deck) ? 1d : 0.55d;
            if ((NeedsMoreDefense(deck) || NeedsMoreDraw(deck)) && deck.EnergySources > 0)
            {
                energyFitMultiplier = Math.Min(energyFitMultiplier, 0.35d);
            }

            score += features.Energy * synergy.EnergyWithHeavyCurveValue * energyFitMultiplier;
        }

        if (features.PersistentStrength > 0 || features.TemporaryStrength > 0 || features.Vulnerable > 0)
        {
            score += Math.Min(deck.AttackCount, 8) * synergy.AttackScalingSynergyPerAttack;
        }

        if (features.PersistentDexterity > 0 || features.TemporaryDexterity > 0)
        {
            score += Math.Min(deck.BlockSources, 8) * synergy.DefenseScalingSynergyPerBlockSource;
        }

        if (card.Type == CardType.Power && deck.DrawSources > 0)
        {
            score += synergy.PowerWithDrawBonus;
        }

        if (card.Retain && deck.HighCostCards > 0)
        {
            score += synergy.RetainWithHighCostBonus;
        }

        if (card.Exhaust && (features.Draw > 0 || features.Energy > 0 || features.TotalKnownValue >= 18))
        {
            score += synergy.ExhaustSynergyBonus;
        }

        if (card.Exhaust && deck.ExhaustPayoffCards > 0)
        {
            score += 4d + Math.Min(deck.ExhaustPayoffCards, 3) * 1.5d;
        }

        if ((features.Draw > 0 || features.Energy > 0) && deck.ExhaustCards >= 3)
        {
            score += Math.Min(deck.ExhaustCards, 6) * 0.8d;
        }

        if ((features.Draw > 0 || card.Exhaust) && deck.BadCards >= 2)
        {
            score += Math.Min(deck.BadCards, 5) * (features.Draw > 0 ? 1.4d : 1.0d);
        }

        score += ScoreArchetypeFit(card, features, context);

        return score;
    }

    private static double ScoreDeckNeeds(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context, AiCardRewardTuning tuning)
    {
        AiCardRewardSynergyWeights synergy = tuning.SynergyWeights;
        DeckSummary deck = context.DeckSummary;
        double score = 0d;

        bool defenseBehind = NeedsMoreDefense(deck);
        bool drawBehind = NeedsMoreDraw(deck);
        bool supportBehind = defenseBehind || drawBehind;
        bool severeDamageDeficit = HasSevereDamageDeficit(deck);
        double offensiveValue = EstimateOffensiveCardValue(card, features);

        if (severeDamageDeficit)
        {
            if (offensiveValue > 0d)
            {
                double emergencyDamageBonus = 12d + Math.Min(offensiveValue, 18d) * 0.9d;
                if (IsDamageOrbCard(card))
                {
                    emergencyDamageBonus += 6d;
                }

                score += emergencyDamageBonus;
            }
            else if ((features.Block + features.Summon) > 0 &&
                     features.Draw == 0 &&
                     features.Weak == 0 &&
                     features.PersistentDexterity == 0)
            {
                score -= 10d;
            }
        }

        if (NeedsMoreDamage(deck) && !supportBehind)
        {
            score += Math.Min(features.Damage / synergy.DamageNeedScale, synergy.DamageNeedCap);
            score += features.Vulnerable * synergy.VulnerableNeedValue;
        }
        else if (NeedsMoreDamage(deck) && IsPremiumDamageCard(card, features))
        {
            score += Math.Min(features.Damage / synergy.DamageNeedScale, synergy.DamageNeedCap) * 0.45d;
            score += features.Vulnerable * synergy.VulnerableNeedValue * 0.5d;
        }

        if (defenseBehind)
        {
            double defenseNeedMultiplier = deck.QualityDefenseSources < DesiredQualityDefenseSources(deck) ? 1.25d : 1d;
            score += Math.Min((features.Block + features.Summon * 1.2d) / synergy.BlockNeedScale, synergy.BlockNeedCap) * defenseNeedMultiplier;
            score += features.Weak * synergy.WeakNeedValue;
            score += features.PersistentDexterity * synergy.DexterityNeedValue;
            if (IsFrostCard(card))
            {
                score += 4d;
            }
        }

        if (drawBehind)
        {
            double drawNeedValue = synergy.DrawNeedValue;
            if (deck.EnergySources > deck.DrawSources)
            {
                drawNeedValue += 3d;
            }

            if (defenseBehind && (features.Block > 0 || features.Summon > 0 || features.Weak > 0))
            {
                drawNeedValue += 1.5d;
            }

            score += features.Draw * drawNeedValue;
        }

        if (NeedsMoreEnergy(deck))
        {
            double energyNeedValue = synergy.EnergyNeedValue;
            if (supportBehind && deck.EnergySources > 0)
            {
                energyNeedValue *= 0.45d;
            }

            score += features.Energy * energyNeedValue;
        }

        if (deck.ScalingSources < DesiredScalingSources(deck))
        {
            score += (features.PersistentStrength + features.PersistentDexterity) * synergy.ScalingNeedValue;
            if (card.Type == CardType.Power)
            {
                score += synergy.PowerScalingBonus;
            }
        }

        if (NeedsAoE(context, deck) && features.DealsAoE)
        {
            score += Math.Min(features.Damage / 2d, 12d) + 6d;
            if (deck.AoESources == 0 && context.CurrentActIndex <= 1)
            {
                score += 6d;
            }
        }

        score += ScoreEarlyElitePreparation(card, features, context);

        if (NeedsBossScaling(context, deck))
        {
            score += (features.PersistentStrength + features.PersistentDexterity) * 4d;
            if (card.Type == CardType.Power)
            {
                score += 6d;
            }

            if (features.Draw > 0 && deck.ScalingSources > 0)
            {
                score += features.Draw * 2d;
            }

            if (IsScalingPayoffCard(card))
            {
                score += 10d;
            }

            if ((IsDefectLikeDeck(context) || IsDefectLikeCard(card)) && IsFocusCard(card) && deck.OrbCards >= 2)
            {
                score += 12d + Math.Min(deck.OrbCards, 6) * 1.4d;
            }

            if ((IsDefectLikeDeck(context) || IsDefectLikeCard(card)) && IsOrbSlotCard(card) && (deck.FocusCards > 0 || deck.OrbCards >= 4))
            {
                score += 7d + Math.Min(deck.FocusCards * 2 + deck.OrbCards, 8) * 0.8d;
            }
        }

        score += ScoreArchetypeNeeds(card, features, context);
        score += ScoreBalanceNeeds(card, features, context);

        if (supportBehind &&
            IsAttackAhead(deck) &&
            card.Type == CardType.Attack &&
            !features.DealsAoE &&
            features.Draw == 0 &&
            features.Block == 0 &&
            features.Weak == 0)
        {
            score -= 4d;
        }

        if (drawBehind &&
            IsEnergyAhead(deck) &&
            features.Energy > 0 &&
            features.Draw == 0 &&
            features.TotalKnownValue <= 10)
        {
            score -= 4d;
        }

        if (deck.BasicCards >= 7 && (features.Draw > 0 || features.Energy > 0 || card.Exhaust))
        {
            score += Math.Min(deck.BasicCards - 6, 5) * 1.2d;
        }

        if (deck.StatusHandlingCards <= 0 &&
            StatusCardStrategy.IsLikelyHandCleanupCard(card))
        {
            double urgency = context.CurrentActIndex == 0
                ? context.ActFloor <= 8 ? 42d : 30d
                : 24d;
            if (deck.BadCards > 0)
            {
                urgency += Math.Min(deck.BadCards, 4) * 7d;
            }

            score += urgency;
            if (features.Draw > 0)
            {
                score += Math.Min(features.Draw, 3) * 3d;
            }
        }

        if (deck.CardCount <= 15 && card.EffectiveCost <= 1)
        {
            score += Math.Min(features.Damage + features.Block, synergy.EarlyCheapCardTempoCap) * synergy.EarlyCheapCardTempoScale;
        }

        return score;
    }

    private static double ScoreRedundancy(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context, AiCardRewardTuning tuning)
    {
        AiCardRewardDisciplineWeights discipline = tuning.DisciplineWeights;
        DeckSummary deck = context.DeckSummary;
        int copiesInDeck = context.DeckCards.Count(deckCard =>
            string.Equals(deckCard.CardId, card.CardId, StringComparison.Ordinal));

        double penalty = copiesInDeck * discipline.DuplicatePenaltyPerCopy;

        if (deck.DrawSources >= DesiredDrawSources(deck) + 1 && features.Draw > 0)
        {
            penalty += features.Draw * discipline.ExcessDrawPenalty * GetEngineExcessPenaltyMultiplier(deck);
        }

        if (deck.EnergySources >= DesiredEnergySources(deck) + 1 && features.Energy > 0)
        {
            penalty += features.Energy * discipline.ExcessEnergyPenalty * GetEnergyExcessPenaltyMultiplier(deck);
        }

        if ((deck.FrontloadDamageSources >= DesiredDamageSources(deck) + 2 ||
             deck.QualityDamageSources >= DesiredQualityDamageSources(deck) + 2) &&
            features.Damage > 0)
        {
            penalty += Math.Min(features.Damage / discipline.ExcessDamagePenaltyScale, discipline.ExcessDamagePenaltyCap);
        }

        if (deck.BlockSources >= DesiredBlockSources(deck) + 2 && features.Block > 0)
        {
            penalty += Math.Min(features.Block / discipline.ExcessBlockPenaltyScale, discipline.ExcessBlockPenaltyCap);
        }

        if (deck.ScalingSources >= DesiredScalingSources(deck) + 2 &&
            (features.PersistentStrength > 0 || features.PersistentDexterity > 0 || card.Type == CardType.Power))
        {
            penalty += discipline.ExcessScalingPenalty;
        }

        if (deck.PowerCount >= 5 && card.Type == CardType.Power)
        {
            penalty += discipline.ExcessPowerPenalty;
        }

        if (deck.AoESources >= 2 && features.DealsAoE && deck.FrontloadDamageSources >= DesiredDamageSources(deck))
        {
            penalty += 5d;
        }

        if (context.TotalFloor > 12 &&
            features.TotalKnownValue <= 10 &&
            card.Type is CardType.Attack or CardType.Skill &&
            !features.DealsAoE &&
            features.Draw == 0 &&
            features.Energy == 0 &&
            features.PersistentStrength == 0 &&
            features.PersistentDexterity == 0)
        {
            penalty += 6d;
        }

        penalty += ScoreArchetypeDriftPenalty(card, features, context);
        penalty += ScoreBalanceRedundancyPenalty(card, features, context);

        if (card.Ethereal && card.EffectiveCost >= 2)
        {
            penalty += discipline.EtherealHighCostPenalty;
        }

        return penalty;
    }

    private static double ScoreBalanceNeeds(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        if (deck.CardCount < 8)
        {
            return 0d;
        }

        bool defenseBehind = NeedsMoreDefense(deck);
        bool drawBehind = NeedsMoreDraw(deck);
        double score = 0d;
        bool severeDamageDeficit = HasSevereDamageDeficit(deck);

        if (defenseBehind)
        {
            score += Math.Min(features.Block + features.Summon, 14) * 0.65d;
            score += features.Weak * 4d;
            score += features.PersistentDexterity * 5d;

            if (IsFrostCard(card))
            {
                score += 5d;
            }

            if (features.Block > 0 && features.Draw > 0)
            {
                score += 5d;
            }

            if (features.Summon > 0)
            {
                score += 5d + Math.Min(features.Summon, 12) * 0.6d;
            }
        }

        if (drawBehind)
        {
            score += features.Draw * (IsEnergyAhead(deck) ? 6d : 3d);
            if (features.Draw > 0 && (features.Block > 0 || features.Weak > 0))
            {
                score += 4d;
            }
        }

        if (severeDamageDeficit)
        {
            if (EstimateOffensiveCardValue(card, features) > 0d)
            {
                score += 8d;
            }
            else if ((features.Block + features.Summon) > 0 && features.Draw == 0 && features.Weak == 0)
            {
                score -= 8d;
            }
        }

        if (IsAttackAhead(deck) && defenseBehind && IsPlainDamageCard(card, features))
        {
            score -= 8d;
        }

        if ((IsEnergyAhead(deck) || !NeedsMoreEnergy(deck)) &&
            (defenseBehind || drawBehind) &&
            IsPureEnergyCard(card, features))
        {
            score -= 10d + features.Energy * 4d;
        }

        return score;
    }

    private static double ScoreBalanceRedundancyPenalty(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        if (deck.CardCount < 8)
        {
            return 0d;
        }

        bool defenseBehind = NeedsMoreDefense(deck);
        bool drawBehind = NeedsMoreDraw(deck);
        double penalty = 0d;

        if (IsAttackAhead(deck) && (defenseBehind || drawBehind) && IsPlainDamageCard(card, features))
        {
            int qualityGap = Math.Max(0, deck.QualityDamageSources - deck.QualityDefenseSources);
            penalty += 8d + Math.Min(qualityGap, 5) * 1.5d;
        }

        if ((IsEnergyAhead(deck) || !NeedsMoreEnergy(deck)) &&
            (defenseBehind || drawBehind) &&
            IsPureEnergyCard(card, features))
        {
            penalty += 12d + features.Energy * 5d;
        }

        if (deck.EnergySources >= deck.DrawSources + 2 && features.Energy > 0 && features.Draw == 0)
        {
            penalty += features.Energy * 4d;
        }

        return penalty;
    }

    private static double ScoreArchetypeFit(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        double score = 0d;

        bool drawEnergyCore = deck.HasDrawEnergyEngine || deck.EngineCards >= 5;
        if (drawEnergyCore)
        {
            if (features.Draw > 0)
            {
                score += features.Draw * (deck.EnergySources >= 2 ? 3.0d : 1.5d);
                score += Math.Min(deck.EnergySources, 5) * 0.7d;
            }

            if (features.Energy > 0 && NeedsEngineEnergy(deck))
            {
                score += features.Energy * (deck.DrawSources >= 2 ? 2.6d : 1.5d);
                score += Math.Min(deck.DrawSources, 5) * 0.45d;
            }
            else if (features.Energy > 0 && IsPureEnergyCard(card, features))
            {
                score -= features.Energy * 4d;
            }

            if (card.EffectiveCost == 0 && features.TotalKnownValue > 0)
            {
                score += 2.5d;
            }

            if (IsRecursionCard(card) || IsEnginePayoffCard(card))
            {
                score += 4d + Math.Min(deck.EngineCards, 6) * 0.8d;
            }
        }

        bool defectLike = IsDefectLikeDeck(context) || IsDefectLikeCard(card);
        if (!defectLike)
        {
            return score;
        }

        bool orbCore = deck.HasOrbEngine || deck.OrbCards >= 2;
        if (orbCore)
        {
            if (IsFocusCard(card))
            {
                score += 14d + Math.Min(deck.OrbCards, 6) * 1.5d;
                if (context.CurrentActIndex >= 1 || context.TotalFloor >= 12)
                {
                    score += 4d;
                }
            }

            if (IsOrbSlotCard(card))
            {
                score += 8d + Math.Min(deck.OrbCards, 6) * 1.2d;
                if (deck.FocusCards > 0)
                {
                    score += 4d;
                }
            }

            if (IsOrbCard(card))
            {
                score += 3d + Math.Min(deck.FocusCards * 2 + deck.OrbSlotCards, 7);
                if (IsFrostCard(card) && deck.BlockSources <= DesiredBlockSources(deck))
                {
                    score += 4d;
                }

                if (IsDamageOrbCard(card) && deck.FrontloadDamageSources <= DesiredDamageSources(deck))
                {
                    score += 3d;
                }
            }
        }

        bool powerCore = deck.HasPowerEngine || deck.PowerCount >= 2 || deck.PowerPayoffCards > 0;
        if (powerCore)
        {
            if (card.Type == CardType.Power)
            {
                score += Math.Min(11d, 3d + deck.PowerPayoffCards * 2d + deck.DrawSources * 0.7d);
                if (IsHighImpactPower(card))
                {
                    score += 6d;
                }
            }

            if (IsPowerPayoffCard(card))
            {
                score += 8d + Math.Min(deck.PowerCount, 4) * 2d;
            }

            if (features.Draw > 0)
            {
                score += Math.Min(deck.PowerCount, 5) * 1.2d;
            }
        }

        if ((context.CurrentActIndex >= 1 || context.TotalFloor >= 12) && IsScalingPayoffCard(card))
        {
            score += 5d;
        }

        return score;
    }

    private static double ScoreArchetypeNeeds(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        double score = 0d;

        if (deck.DrawSources >= 3 && NeedsEngineEnergy(deck) && features.Energy > 0)
        {
            score += features.Energy * 2.5d;
        }

        if (IsEnergyAhead(deck) && deck.DrawSources < DesiredDrawSources(deck) + 1 && features.Draw > 0)
        {
            score += features.Draw * 4d;
        }

        if (!IsDefectLikeDeck(context) && !IsDefectLikeCard(card))
        {
            return score;
        }

        if ((context.CurrentActIndex >= 1 || context.TotalFloor >= 12) &&
            deck.OrbCards >= 2 &&
            deck.FocusCards <= 1 &&
            IsFocusCard(card))
        {
            score += 8d + (deck.FocusCards == 0 ? 6d : 0d) + Math.Min(deck.OrbCards, 6) * 1.2d;
        }

        if ((deck.OrbCards >= 4 || deck.FocusCards > 0) &&
            deck.OrbSlotCards <= 1 &&
            IsOrbSlotCard(card))
        {
            score += deck.OrbSlotCards == 0 ? 8d : 3d;
        }

        if (deck.PowerCount >= 3 && deck.DrawSources < 4 && features.Draw > 0)
        {
            score += features.Draw * 2d;
        }

        return score;
    }

    private static double ScoreArchetypeDriftPenalty(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        if (deck.CardCount < 18 ||
            (!deck.HasDrawEnergyEngine && !deck.HasOrbEngine && !deck.HasPowerEngine && deck.EngineCards < 5))
        {
            return 0d;
        }

        if (IsArchetypeCard(card, features) ||
            (features.Damage > 0 && deck.FrontloadDamageSources < DesiredDamageSources(deck)) ||
            (features.Block + features.Summon > 0 && deck.BlockSources < DesiredBlockSources(deck)))
        {
            return 0d;
        }

        if (card.Type is not (CardType.Attack or CardType.Skill) ||
            features.TotalKnownValue > 14 ||
            features.DealsAoE ||
            features.Vulnerable > 0 ||
            features.Weak > 0)
        {
            return 0d;
        }

        double phasePenalty = context.CurrentActIndex >= 1 || context.TotalFloor >= 12 ? 5d : 2d;
        return phasePenalty + Math.Min(deck.CardCount - 17, 7) * 0.7d;
    }

    private static double GetEngineExcessPenaltyMultiplier(DeckSummary deck)
    {
        if (deck.HasDrawEnergyEngine || deck.EngineCards >= 6)
        {
            return 0.35d;
        }

        if (deck.DrawSources >= 3 || deck.EnergySources >= 3)
        {
            return 0.6d;
        }

        return 1d;
    }

    private static double GetEnergyExcessPenaltyMultiplier(DeckSummary deck)
    {
        if (deck.DrawSources >= deck.EnergySources + 2)
        {
            return 0.65d;
        }

        if (deck.EnergySources >= deck.DrawSources + 1 || NeedsMoreDefense(deck))
        {
            return 1.25d;
        }

        return 1d;
    }

    private static bool IsPremiumDamageCard(ResolvedCardView card, CardFeatureVector features)
    {
        return features.DealsAoE ||
               features.Vulnerable > 0 ||
               features.Damage >= 14 ||
               card.EffectiveCost <= 1 && features.Damage >= 10;
    }

    private static bool IsPlainDamageCard(ResolvedCardView card, CardFeatureVector features)
    {
        return card.Type == CardType.Attack &&
               features.Damage > 0 &&
               !features.DealsAoE &&
               features.Draw == 0 &&
               features.Energy == 0 &&
               features.Summon == 0 &&
               features.Vulnerable == 0 &&
               features.Weak == 0 &&
               features.PersistentStrength == 0 &&
               features.PersistentDexterity == 0;
    }

    private static bool IsPureEnergyCard(ResolvedCardView card, CardFeatureVector features)
    {
        return features.Energy > 0 &&
               features.Draw == 0 &&
               features.Damage == 0 &&
               features.Block == 0 &&
               features.Summon == 0 &&
               features.Vulnerable == 0 &&
               features.Weak == 0 &&
               features.PersistentStrength == 0 &&
               features.PersistentDexterity == 0 &&
               card.Type != CardType.Power;
    }

    private static bool IsArchetypeCard(ResolvedCardView card, CardFeatureVector features)
    {
        return features.Draw > 0 ||
               features.Energy > 0 ||
               features.Summon > 0 ||
               features.PersistentStrength > 0 ||
               features.PersistentDexterity > 0 ||
               card.Type == CardType.Power ||
               card.EffectiveCost == 0 ||
               IsOrbCard(card) ||
               IsFocusCard(card) ||
               IsOrbSlotCard(card) ||
               IsPowerPayoffCard(card) ||
               IsRecursionCard(card) ||
               IsEnginePayoffCard(card);
    }

    private static bool IsDefectLikeDeck(CardEvaluationContext context)
    {
        return context.DeckCards.Any(IsDefectLikeCard);
    }

    private static bool IsDefectLikeCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "DEFECT",
            "ZAP",
            "DUALCAST",
            "DUAL_CAST",
            "ORB",
            "FOCUS",
            "LIGHTNING",
            "FROST",
            "DARKNESS",
            "PLASMA",
            "COOLHEADED",
            "COOL_HEADED",
            "DEFRAGMENT",
            "CAPACITOR",
            "STORM",
            "TURBO",
            "HYPERBEAM",
            "BEAM_CELL",
            "SWEEPING_BEAM",
            "GO_FOR_THE_EYES",
            "CHARGE_BATTERY",
            "SHATTER",
            "ITERATION",
            "ENERGY_SURGE");
    }

    private static bool IsOrbCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "ORB",
            "CHANNEL",
            "EVOKE",
            "LIGHTNING",
            "FROST",
            "DARK",
            "PLASMA",
            "ZAP",
            "DUALCAST",
            "DUAL_CAST",
            "BALL_LIGHTNING",
            "COLD_SNAP",
            "COOLHEADED",
            "COOL_HEADED",
            "GLACIER",
            "CHAOS",
            "DARKNESS",
            "RAINBOW",
            "TEMPEST",
            "MULTI_CAST",
            "BARRAGE",
            "SHATTER",
            "RECURSION",
            "ICE_LANCE");
    }

    private static bool IsFocusCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "FOCUS",
            "DEFRAGMENT",
            "DE_FRAGMENT",
            "CONSUME",
            "BIAS_COGNITION",
            "BIASED_COGNITION");
    }

    private static bool IsOrbSlotCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "CAPACITOR",
            "ORB_SLOT",
            "ORBSLOT");
    }

    private static bool IsPowerPayoffCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "STORM",
            "HEATSINK",
            "HEAT_SINK",
            "CREATIVE_AI",
            "ECHO_FORM",
            "MACHINE_LEARNING",
            "ELECTRODYNAMICS");
    }

    private static bool IsRecursionCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "HOLOGRAM",
            "RECURSION",
            "ALL_FOR_ONE",
            "SEEK",
            "REBOOT",
            "SCAVENGE",
            "ITERATION");
    }

    private static bool IsEnginePayoffCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "REBOOT",
            "ALL_FOR_ONE",
            "HOLOGRAM",
            "SKIM",
            "SCRAPE",
            "COMPILE_DRIVER",
            "SWEEPING_BEAM",
            "TURBO",
            "DOUBLE_ENERGY",
            "ENERGY_SURGE",
            "ITERATION",
            "BOOST_AWAY");
    }

    private static bool IsFrostCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "FROST",
            "COLD_SNAP",
            "COOLHEADED",
            "COOL_HEADED",
            "GLACIER",
            "ICE_LANCE");
    }

    private static bool IsDamageOrbCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "LIGHTNING",
            "DARK",
            "BALL_LIGHTNING",
            "DARKNESS",
            "TEMPEST",
            "SHATTER");
    }

    private static bool IsHighImpactPower(ResolvedCardView card)
    {
        return card.Type == CardType.Power &&
               (card.Rarity == "Rare" ||
                IsCardIdLikeAny(card,
                    "DEFRAGMENT",
                    "DE_FRAGMENT",
                    "ECHO_FORM",
                    "CREATIVE_AI",
                    "MACHINE_LEARNING",
                    "ELECTRODYNAMICS",
                    "STORM",
                    "CAPACITOR"));
    }

    private static bool IsScalingPayoffCard(ResolvedCardView card)
    {
        return IsFocusCard(card) ||
               IsOrbSlotCard(card) ||
               IsPowerPayoffCard(card) ||
               IsCardIdLikeAny(card,
                   "ECHO_FORM",
                   "CREATIVE_AI",
                   "MACHINE_LEARNING",
                   "BIASED_COGNITION",
                   "BIAS_COGNITION",
                   "CONSUME");
    }

    private static bool IsCardIdLikeAny(ResolvedCardView card, params string[] tokens)
    {
        return tokens.Any(token => IsCardIdLike(card, token));
    }

    private static bool IsCardIdLike(ResolvedCardView card, string token)
    {
        string normalizedId = NormalizeCardToken(card.CardId);
        string normalizedName = NormalizeCardToken(card.Name);
        string normalizedToken = NormalizeCardToken(token);
        return normalizedId.Contains(normalizedToken, StringComparison.Ordinal) ||
               normalizedName.Contains(normalizedToken, StringComparison.Ordinal);
    }

    private static string NormalizeCardToken(string value)
    {
        return value.Replace(' ', '_').Replace('-', '_').Replace(':', '_').Replace('/', '_').ToUpperInvariant();
    }

    private static double ScoreContext(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context, AiCardRewardTuning tuning)
    {
        AiCardRewardDisciplineWeights discipline = tuning.DisciplineWeights;
        AiCardRewardSynergyWeights synergy = tuning.SynergyWeights;
        double score = context.ChoiceSource switch
        {
            CardChoiceSource.Reward => discipline.RewardContextBonus,
            CardChoiceSource.ChooseScreen => discipline.ChooseScreenContextBonus,
            CardChoiceSource.Event => discipline.EventContextBonus,
            CardChoiceSource.Shop => ScoreShopContext(context, tuning),
            _ => 0d
        };

        if (context.CurrentActIndex == 0 && context.TotalFloor <= 10)
        {
            score += Math.Min(features.Damage + features.Block, synergy.EarlyActTempoCap) * synergy.EarlyActTempoScale;
        }

        if (context.AscensionLevel >= 10 && features.Block > 0)
        {
            score += synergy.HighAscensionBlockBonus;
        }

        score += ScoreMultiplayerCardPreference(card, context);
        score += ScoreEnemyDebuffPreference(card, features, context);

        return score;
    }

    private static double ScoreMultiplayerCardPreference(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!card.IsMultiplayerOnlyCard() ||
            context.Player.RunState.Players.Count <= 1)
        {
            return 0d;
        }

        double score = 13d;
        if (context.ChoiceSource == CardChoiceSource.Shop)
        {
            score += 4d;
        }

        if (context.CurrentActIndex <= 1)
        {
            score += 3d;
        }

        if (card.Rarity == "Rare")
        {
            score += 2d;
        }

        return score;
    }

    private static double ScoreEnemyDebuffPreference(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        int debuffAmount = features.Vulnerable + features.Weak + features.Poison;
        if (debuffAmount <= 0)
        {
            return 0d;
        }

        DeckSummary deck = context.DeckSummary;
        double score = 2d + features.Vulnerable * 1.8d + features.Weak * 1.6d + features.Poison * 1.35d;
        if (card.AppliesVulnerableToAllEnemies() || card.AppliesWeakToAllEnemies() || card.AppliesPoisonToAllEnemies())
        {
            score += 2d;
        }

        if (features.Poison > 0 && (context.CurrentActIndex >= 1 || context.TotalFloor >= 8))
        {
            score += Math.Min(7d, 2d + features.Poison * 0.55d);
        }

        int debuffSources = deck.VulnerableSources + deck.WeakSources;
        if (debuffSources == 0)
        {
            score += 4d;
        }
        else if (debuffSources <= 2)
        {
            score += 2d;
        }

        if (context.CurrentActIndex == 0 && context.TotalFloor <= 12)
        {
            score += 1.5d;
        }

        return score;
    }

    private static double ScoreRunPhaseStrategy(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        double score = 0d;

        if (context.CurrentActIndex == 0 && context.TotalFloor <= 6)
        {
            if (card.Type == CardType.Attack && features.Damage >= 9)
            {
                double attackTempoBonus = deck.FrontloadDamageSources < 6 ? 7d : 3d;
                if (NeedsMoreDefense(deck) &&
                    deck.QualityDamageSources >= DesiredQualityDamageSources(deck))
                {
                    attackTempoBonus *= 0.45d;
                }

                score += attackTempoBonus;
            }

            if (features.DealsAoE && deck.AoESources == 0)
            {
                score += 9d;
            }

            if (card.Type == CardType.Power && features.TotalKnownValue <= 8)
            {
                score -= 4d;
            }
        }

        if (deck.StatusHandlingCards <= 0 &&
            StatusCardStrategy.IsLikelyHandCleanupCard(card))
        {
            score += context.CurrentActIndex == 0
                ? context.TotalFloor <= 12 ? 24d : 14d
                : 10d;
        }

        if (context.CurrentActIndex >= 1 || context.ActFloor >= 10)
        {
            if (features.DealsAoE && deck.AoESources <= 1)
            {
                score += deck.AoESources == 0 ? 8d : 3d;
            }

            if (deck.ScalingSources <= 1 &&
                (features.PersistentStrength > 0 || features.PersistentDexterity > 0 || card.Type == CardType.Power))
            {
                score += 6d;
            }

            if (features.Draw > 0 && deck.CardCount >= 18)
            {
                score += Math.Min(deck.CardCount - 16, 8) * 0.8d;
            }

            if (NeedsMoreDefense(deck) &&
                card.Type == CardType.Attack &&
                !features.DealsAoE &&
                features.Block == 0 &&
                features.Weak == 0 &&
                features.Draw == 0)
            {
                score -= 5d;
            }
        }

        if (deck.FrontloadDamageSources >= DesiredDamageSources(deck) + 2 &&
            card.Type == CardType.Attack &&
            !features.DealsAoE &&
            features.Damage <= 11 &&
            features.Draw == 0 &&
            features.Vulnerable == 0)
        {
            score -= 7d;
        }

        if (deck.BlockSources >= DesiredBlockSources(deck) + 2 &&
            card.Type == CardType.Skill &&
            features.Block <= 8 &&
            features.Draw == 0 &&
            features.Weak == 0 &&
            features.PersistentDexterity == 0)
        {
            score -= 5d;
        }

        return score;
    }

    private static double ScoreFutureRewardAwareness(ResolvedCardView card, CardEvaluationContext context)
    {
        FutureRewardRouteEvaluation? futureRewards = context.FutureRewards;
        if (futureRewards == null ||
            futureRewards.CardRewards.Count == 0 && futureRewards.ShopRewards.Count == 0 && futureRewards.RelicRewards.Count == 0)
        {
            return 0d;
        }

        bool appearsAsBestFutureCard = futureRewards.CardRewards.Any(reward =>
            string.Equals(reward.BestCardId, card.CardId, StringComparison.Ordinal));
        if (appearsAsBestFutureCard)
        {
            return -5d;
        }

        bool appearsInFutureOffer = futureRewards.CardRewards.Any(reward =>
            reward.CardIds.Any(cardId => string.Equals(cardId, card.CardId, StringComparison.Ordinal)));
        bool appearsInFutureShop = futureRewards.ShopRewards.Any(shop =>
            shop.CardIds.Any(cardId => string.Equals(cardId, card.CardId, StringComparison.Ordinal)));
        bool strongFutureShopAhead = futureRewards.ShopRewards.Any(static shop => shop.RewardValue >= 25d);
        double futureStrengthPenalty = context.SkipAllowed
            ? Math.Min(futureRewards.RewardValue * 0.03d, 4d)
            : 0d;
        double shopPenalty = appearsInFutureShop ? 3d : strongFutureShopAhead && IsPlainDamageCard(card, CardFeatureVector.From(card)) ? 1.5d : 0d;
        return (appearsInFutureOffer ? -2d : 0d) - shopPenalty - futureStrengthPenalty;
    }

    private static double ScoreShopContext(CardEvaluationContext context, AiCardRewardTuning tuning)
    {
        AiCardRewardDisciplineWeights discipline = tuning.DisciplineWeights;
        if (!context.CandidateGoldCost.HasValue)
        {
            return -discipline.ShopMissingCostPenalty;
        }

        double gold = Math.Max(context.Gold, 1);
        double cost = context.CandidateGoldCost.Value;
        return -(cost / Math.Max(gold, 50d)) * discipline.ShopCostRatioPenaltyScale;
    }

    private static double GetSkipThreshold(CardEvaluationContext context, AiCardRewardTuning tuning)
    {
        AiCardRewardDisciplineWeights discipline = tuning.DisciplineWeights;
        if (!context.SkipAllowed || context.ChoiceSource == CardChoiceSource.ForcedChoice)
        {
            return double.NegativeInfinity;
        }

        return context.ChoiceSource switch
        {
            CardChoiceSource.Reward => discipline.RewardSkipThreshold,
            CardChoiceSource.ChooseScreen => discipline.ChooseScreenSkipThreshold,
            CardChoiceSource.Event => discipline.EventSkipThreshold,
            CardChoiceSource.Shop => discipline.ShopSkipThresholdBase + (context.CandidateGoldCost ?? 0) * discipline.ShopSkipThresholdCostFactor,
            _ => discipline.EventSkipThreshold
        };
    }

    private static int DesiredDamageSources(DeckSummary deck)
    {
        if (deck.CardCount < 12)
        {
            return 5;
        }

        return deck.CardCount < 20 ? 6 : 7;
    }

    private static int DesiredQualityDamageSources(DeckSummary deck)
    {
        if (deck.CardCount < 12)
        {
            return 2;
        }

        return deck.CardCount < 20 ? 3 : 4;
    }

    private static int DesiredBlockSources(DeckSummary deck)
    {
        return deck.CardCount < 15 ? 5 : 7;
    }

    private static int DesiredQualityDefenseSources(DeckSummary deck)
    {
        if (deck.CardCount < 12)
        {
            return 2;
        }

        return deck.CardCount < 20 ? 4 : 6;
    }

    private static int DesiredDrawSources(DeckSummary deck)
    {
        if (deck.CardCount < 18)
        {
            return 2;
        }

        return deck.CardCount < 26 ? 3 : 4;
    }

    private static int DesiredEnergySources(DeckSummary deck)
    {
        return deck.CardCount < 18 ? 1 : 2;
    }

    private static int DesiredScalingSources(DeckSummary deck)
    {
        return deck.CardCount < 15 ? 1 : 2;
    }

    private static bool NeedsMoreDamage(DeckSummary deck)
    {
        return deck.FrontloadDamageSources < DesiredDamageSources(deck) ||
               deck.QualityDamageSources < DesiredQualityDamageSources(deck);
    }

    private static bool HasSevereDamageDeficit(DeckSummary deck)
    {
        return deck.FrontloadDamageSources <= Math.Max(2, DesiredDamageSources(deck) - 3) ||
               deck.QualityDamageSources == 0 && deck.FrontloadDamageSources < DesiredDamageSources(deck);
    }

    private static double EstimateOffensiveCardValue(ResolvedCardView card, CardFeatureVector features)
    {
        double value = features.Damage + features.Vulnerable * 4d;
        if (IsDamageOrbCard(card))
        {
            value += 8d;
        }

        if (features.DealsAoE && value > 0d)
        {
            value += 4d;
        }

        return value;
    }

    private static bool NeedsMoreDefense(DeckSummary deck)
    {
        return deck.BlockSources < DesiredBlockSources(deck) ||
               deck.QualityDefenseSources < DesiredQualityDefenseSources(deck);
    }

    private static bool NeedsMoreDraw(DeckSummary deck)
    {
        return deck.DrawSources < DesiredDrawSources(deck);
    }

    private static bool NeedsMoreEnergy(DeckSummary deck)
    {
        return deck.EnergySources < DesiredEnergySources(deck);
    }

    private static bool NeedsEngineEnergy(DeckSummary deck)
    {
        int desired = DesiredEnergySources(deck);
        if (deck.CardCount >= 20 && deck.DrawSources >= 4)
        {
            desired++;
        }

        return deck.EnergySources < desired || deck.DrawSources >= deck.EnergySources + 3;
    }

    private static bool IsAttackAhead(DeckSummary deck)
    {
        return deck.FrontloadDamageSources >= DesiredDamageSources(deck) + 1 ||
               deck.QualityDamageSources >= DesiredQualityDamageSources(deck) + 1 ||
               deck.AttackCount >= deck.SkillCount + 3;
    }

    private static bool IsEnergyAhead(DeckSummary deck)
    {
        return deck.EnergySources >= DesiredEnergySources(deck) + 1 ||
               deck.EnergySources >= 2 && deck.EnergySources > deck.DrawSources;
    }

    private static bool NeedsAoE(CardEvaluationContext context, DeckSummary deck)
    {
        return deck.AoESources == 0 &&
               (context.CurrentActIndex <= 1 || context.TotalFloor <= 18);
    }

    private static bool NeedsBossScaling(CardEvaluationContext context, DeckSummary deck)
    {
        return deck.ScalingSources < DesiredScalingSources(deck) &&
               (context.ActFloor >= 10 || context.CurrentActIndex >= 1 || context.TotalFloor >= 12);
    }

    private static double ScoreEarlyElitePreparation(ResolvedCardView card, CardFeatureVector features, CardEvaluationContext context)
    {
        if (context.CurrentActIndex != 0 || context.TotalFloor > 13)
        {
            return 0d;
        }

        DeckSummary deck = context.DeckSummary;
        double score = 0d;
        if (deck.AoESources <= 1 && features.DealsAoE)
        {
            score += deck.AoESources == 0 ? 8d : 3d;
        }

        if (deck.QualityDefenseSources < DesiredQualityDefenseSources(deck) &&
            (features.Block > 0 || features.Weak > 0))
        {
            score += Math.Min(features.Block, 14) * 0.35d + features.Weak * 3d;
        }

        if (deck.VulnerableSources + deck.WeakSources <= 1 &&
            (features.Vulnerable > 0 || features.Weak > 0))
        {
            score += 3d + Math.Min(features.Vulnerable + features.Weak, 3) * 1.5d;
        }

        if (NeedsMoreDefense(deck) &&
            IsPlainDamageCard(card, features))
        {
            score -= 4d;
        }

        return score;
    }

    private static double GetRarityBonus(string rarity, AiCardRewardIntrinsicWeights intrinsic)
    {
        return rarity switch
        {
            "Rare" => intrinsic.RareBonus,
            "Uncommon" => intrinsic.UncommonBonus,
            "Common" => 0d,
            "Basic" => intrinsic.BasicBonus,
            "Curse" => intrinsic.CursePenalty,
            "Status" => intrinsic.StatusPenalty,
            "Quest" => intrinsic.QuestPenalty,
            "Event" => intrinsic.EventBonus,
            "Ancient" => intrinsic.AncientBonus,
            _ => 0d
        };
    }

    private readonly record struct CardFeatureVector(
        int Damage,
        int Block,
        int Summon,
        int Draw,
        int Energy,
        int Vulnerable,
        int Weak,
        int Poison,
        int PersistentStrength,
        int PersistentDexterity,
        int TemporaryStrength,
        int TemporaryDexterity,
        int SpecialUtility,
        bool DealsAoE,
        int RepeatCount)
    {
        public int TotalKnownValue =>
            Damage +
            Block +
            (Summon * 2) +
            (Draw * 4) +
            (Energy * 5) +
            (Vulnerable * 3) +
            (Weak * 3) +
            (Poison * 3) +
            (PersistentStrength * 3) +
            (PersistentDexterity * 3) +
            (TemporaryStrength * 2) +
            (TemporaryDexterity * 2) +
            SpecialUtility;

        public static CardFeatureVector From(ResolvedCardView card)
        {
            int temporaryStrength = card.GetSelfTemporaryStrengthAmount();
            int temporaryDexterity = card.GetSelfTemporaryDexterityAmount();

            return new CardFeatureVector(
                card.GetEstimatedDamage(),
                card.GetEstimatedBlock(),
                card.GetSummonAmount(),
                card.GetCardsDrawn(),
                card.GetEnergyGain(),
                card.GetEnemyVulnerableAmount(),
                card.GetEnemyWeakAmount(),
                card.GetEnemyPoisonAmount(),
                Math.Max(0, card.GetSelfStrengthAmount() - temporaryStrength),
                Math.Max(0, card.GetSelfDexterityAmount() - temporaryDexterity),
                temporaryStrength,
                temporaryDexterity,
                SpecialCardEffectHeuristics.EstimateCardSelectionUtility(card) + card.GetRecognizedUtilityAmount(),
                card.DealsDamageToAllEnemies(),
                card.GetReplayMultiplier());
        }
    }
}
