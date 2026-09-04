using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class EventValuationHelpers
{
    public double EvaluateRelic(string? relicId, string? rarity, EventVisitState snapshot, List<string> reasons, AiEventTuning tuning)
    {
        double totalScore = rarity switch
        {
            "Ancient" => tuning.RelicWeights.AncientBaseline,
            "Rare" => tuning.RelicWeights.RareBaseline,
            "Uncommon" => tuning.RelicWeights.UncommonBaseline,
            "Common" => tuning.RelicWeights.CommonBaseline,
            _ => tuning.RelicWeights.FallbackBaseline
        };

        reasons.Add($"relicBaseline={totalScore:F1}");
        if (string.IsNullOrEmpty(relicId))
        {
            return totalScore * tuning.OutcomeWeights.RelicRewardMultiplier;
        }

        string upper = relicId.ToUpperInvariant();
        AddRelicPatternBonus("MEMBERSHIP", 18d, "membership discount scales future shops");
        AddRelicPatternBonus("COURIER", 15d, "courier discount/restock premium");
        AddRelicPatternBonus("BAG_OF_PREPARATION", 11d, "opening draw consistency");
        AddRelicPatternBonus("ANCHOR", 9d, "reliable early block");
        AddRelicPatternBonus("ORICHALCUM", 8d, "passive defense floor");
        AddRelicPatternBonus("PANTOGRAPH", 8d, "boss sustain value");
        AddRelicPatternBonus("VAJRA", 6d, "passive attack scaling");

        if (snapshot.RelicIds.Contains(upper))
        {
            totalScore -= tuning.RelicWeights.DuplicateOwnedPenalty;
            reasons.Add($"duplicate or already-owned effect penalty -{tuning.RelicWeights.DuplicateOwnedPenalty:F1}");
        }

        return totalScore * tuning.OutcomeWeights.RelicRewardMultiplier;

        void AddRelicPatternBonus(string pattern, double bonus, string reason)
        {
            if (upper.Contains(pattern, StringComparison.Ordinal))
            {
                double scaledBonus = bonus * tuning.RelicWeights.SpecialRelicBonusMultiplier;
                totalScore += scaledBonus;
                reasons.Add($"{reason} +{scaledBonus:F1}");
            }
        }
    }

    public double EvaluatePotion(
        string? potionId,
        string? rarity,
        int count,
        EventVisitState snapshot,
        List<string> reasons,
        AiEventTuning tuning)
    {
        return PotionHeuristicEvaluator.EvaluateAcquisitionScore(
            snapshot.Player,
            snapshot.DeckSummary,
            potionId,
            rarity,
            count,
            snapshot.Player.HasOpenPotionSlots,
            snapshot.RelicIds.Contains("SOZU"),
            reasons) * tuning.OutcomeWeights.PotionRewardMultiplier;
    }

    public double EvaluateFixedCardGain(IEnumerable<string> cardIds, EventVisitState snapshot, List<string> reasons, AiEventTuning tuning)
    {
        double total = 0d;
        foreach (string cardId in cardIds)
        {
            if (!CardCatalogRepository.Shared.TryGet(cardId, out CardCatalogEntry? entry) || entry == null)
            {
                double fallbackScore = 10d * tuning.OutcomeWeights.FixedCardRewardMultiplier;
                total += fallbackScore;
                reasons.Add($"fixedCard={cardId} conservativeBaseline +{fallbackScore:F1}");
                continue;
            }

            double score = EvaluateCatalogCard(entry) * tuning.OutcomeWeights.FixedCardRewardMultiplier;
            total += score;
            reasons.Add($"fixedCard={entry.CardId} cardEval={score:F1}");
        }

        return total;
    }

    public EventRemovalCandidate? SelectBestRemovalCandidate(EventVisitState snapshot)
    {
        return SelectBestRemovalCandidates(snapshot, 1).FirstOrDefault();
    }

    public IReadOnlyList<EventRemovalCandidate> SelectBestRemovalCandidates(EventVisitState snapshot, int count)
    {
        Dictionary<string, int> copiesById = snapshot.Player.Deck.Cards
            .GroupBy(static card => card.Id.Entry, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        List<EventRemovalCandidate> candidates = [];
        for (int index = 0; index < snapshot.Player.Deck.Cards.Count; index++)
        {
            CardModel deckCard = snapshot.Player.Deck.Cards[index];
            if (!deckCard.IsRemovable)
            {
                continue;
            }

            ResolvedCardView resolved = snapshot.DeckCards[index];
            double burden = 0d;
            List<string> reasons = [];
            if (!CardRemovalSafetyPolicy.CanRemoveFromDeck(resolved, snapshot.DeckCards, out _))
            {
                continue;
            }

            switch (resolved.Rarity)
            {
                case "Curse":
                    burden += 40d;
                    reasons.Add("curse tax +40.0");
                    break;
                case "Status":
                    burden += 28d;
                    reasons.Add("status tax +28.0");
                    break;
                case "Basic":
                    burden += 9d;
                    reasons.Add("basic card tax +9.0");
                    break;
                case "Rare":
                    burden -= 6d;
                    reasons.Add("rare keep bias -6.0");
                    break;
                case "Uncommon":
                    burden -= 2d;
                    reasons.Add("uncommon keep bias -2.0");
                    break;
            }

            if (deckCard.Id.Entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase))
            {
                burden += 14d;
                reasons.Add("starter strike burden +14.0");
            }

            if (deckCard.Id.Entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase))
            {
                burden += 11d;
                reasons.Add("starter defend burden +11.0");
            }

            if (copiesById.TryGetValue(deckCard.Id.Entry, out int copies) && copies > 1)
            {
                double duplicatePenalty = (copies - 1) * 3d;
                burden += duplicatePenalty;
                reasons.Add($"duplicate copies +{duplicatePenalty:F1}");
            }

            int knownValue = resolved.GetEstimatedDamage() +
                             resolved.GetEstimatedProtection() +
                             (resolved.GetCardsDrawn() * 4) +
                             (resolved.GetEnergyGain() * 5) +
                             (resolved.GetEnemyVulnerableAmount() * 3) +
                             (resolved.GetEnemyWeakAmount() * 3) +
                             (resolved.GetSelfStrengthAmount() * 3) +
                             (resolved.GetSelfDexterityAmount() * 3);

            if (knownValue <= 0)
            {
                burden += 8d;
                reasons.Add("low known output +8.0");
            }
            else if (knownValue <= 8)
            {
                burden += 4d;
                reasons.Add("thin output +4.0");
            }
            else if (knownValue >= 18)
            {
                burden -= 4d;
                reasons.Add("strong output keep bias -4.0");
            }

            if (resolved.EffectiveCost >= 2 && knownValue <= 10)
            {
                burden += 7d;
                reasons.Add("expensive for output +7.0");
            }

            if (resolved.Ethereal)
            {
                burden += 6d;
                reasons.Add("ethereal reliability tax +6.0");
            }

            if (resolved.Exhaust && knownValue <= 12)
            {
                burden += 2d;
                reasons.Add("low-value exhaust tax +2.0");
            }

            if (resolved.IsUpgraded)
            {
                burden -= 6d;
                reasons.Add("upgraded keep bias -6.0");
            }

            if (resolved.Type == CardType.Power)
            {
                burden -= 3d;
                reasons.Add("power keep bias -3.0");
            }

            if (resolved.EffectiveCost == 0)
            {
                burden -= 2d;
                reasons.Add("zero-cost flexibility keep bias -2.0");
            }

            burden = CardRemovalSafetyPolicy.ApplyDeckRoleProtection(
                resolved,
                snapshot.DeckCards,
                snapshot.Player,
                burden,
                reasons);
            if (!CardRemovalSafetyPolicy.IsWorthwhileRemovalBurden(burden))
            {
                continue;
            }

            candidates.Add(new EventRemovalCandidate
            {
                CardId = deckCard.Id.Entry,
                Name = deckCard.Title?.ToString() ?? deckCard.Id.Entry,
                BurdenScore = burden,
                RuntimeCard = deckCard,
                Reasons = reasons
            });
        }

        return candidates
            .OrderByDescending(static candidate => candidate.BurdenScore)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Take(Math.Max(1, count))
            .ToList();
    }

    public double EvaluateBestUpgradeTarget(EventVisitState snapshot, int count, List<string> reasons, AiEventTuning tuning)
    {
        List<(string CardId, string Name, double Score, List<string> Reasons)> candidates = [];
        foreach (CardModel card in snapshot.Player.Deck.Cards.Where(static card => card.IsUpgradable))
        {
            double score = 0d;
            List<string> candidateReasons = [];
            if (!CardCatalogRepository.Shared.TryGet(card.Id.Entry, out CardCatalogEntry? entry) || entry == null)
            {
                score = 8d;
                candidateReasons.Add("missing catalog entry fallback +8.0");
            }
            else
            {
                score += EvaluateUpgradeSpec(entry.UpgradeSpec, candidateReasons, tuning);
                if (entry.Rarity == "Basic")
                {
                    score += tuning.OutcomeWeights.UpgradeBasicCardBonus;
                    candidateReasons.Add($"basic card cleanup/value +{tuning.OutcomeWeights.UpgradeBasicCardBonus:F1}");
                }

                if (entry.Type == CardType.Power)
                {
                    score += tuning.OutcomeWeights.UpgradePowerCardBonus;
                    candidateReasons.Add($"power upgrade bias +{tuning.OutcomeWeights.UpgradePowerCardBonus:F1}");
                }
            }

            candidates.Add((card.Id.Entry, card.Title?.ToString() ?? card.Id.Entry, score, candidateReasons));
        }

        List<(string CardId, string Name, double Score, List<string> Reasons)> best = candidates
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Take(Math.Max(1, count))
            .ToList();

        double total = best.Sum(static candidate => candidate.Score) * tuning.OutcomeWeights.UpgradeRewardMultiplier;
        foreach (var candidate in best)
        {
            reasons.Add($"bestUpgradeTarget={candidate.CardId} score={candidate.Score:F1} detail=[{string.Join(", ", candidate.Reasons)}]");
        }

        return total;
    }

    public double EvaluateTransform(EventVisitState snapshot, int count, List<string> reasons, AiEventTuning tuning)
    {
        int requiredCount = Math.Max(1, count);
        IReadOnlyList<EventRemovalCandidate> removalCandidates = SelectBestRemovalCandidates(snapshot, requiredCount);
        if (removalCandidates.Count < requiredCount)
        {
            reasons.Add($"not enough safe transform targets candidates={removalCandidates.Count} required={requiredCount}");
            return -5000d;
        }

        double removalValue = removalCandidates.Sum(static candidate => candidate.BurdenScore);
        double expectedReplacementValue = tuning.OutcomeWeights.TransformReplacementBaselinePerCard * Math.Max(1, count);
        double total = ((removalValue * tuning.OutcomeWeights.TransformRemovalValueMultiplier) + expectedReplacementValue) *
                       tuning.OutcomeWeights.TransformRewardMultiplier;
        reasons.Add($"transformRemovalValue={(removalValue * tuning.OutcomeWeights.TransformRemovalValueMultiplier):F1}");
        reasons.Add($"transformReplacementBaseline={expectedReplacementValue:F1}");
        foreach (EventRemovalCandidate removalCandidate in removalCandidates)
        {
            reasons.Add($"transformTarget={removalCandidate.CardId}");
        }

        return total;
    }

    public double EvaluateHpPenalty(EventVisitState snapshot, int hpLoss, bool willKillPlayer, List<string> reasons, AiEventTuning tuning)
    {
        if (hpLoss <= 0)
        {
            return 0d;
        }

        double hpRatio = snapshot.MaxHp > 0 ? (double)snapshot.CurrentHp / snapshot.MaxHp : 0d;
        double perHpPenalty = hpRatio switch
        {
            <= 0.25d => tuning.RiskProfile.HpPenaltyCriticalPerPoint,
            <= 0.40d => tuning.RiskProfile.HpPenaltyLowPerPoint,
            <= 0.60d => tuning.RiskProfile.HpPenaltyMidPerPoint,
            _ => tuning.RiskProfile.HpPenaltyHealthyPerPoint
        };
        double total = hpLoss * perHpPenalty;
        reasons.Add($"hpPenaltyPerPoint={perHpPenalty:F1}");
        reasons.Add($"hpPenaltyTotal={-total:F1}");

        if (willKillPlayer)
        {
            total += tuning.RiskProfile.LethalOptionPenalty;
            reasons.Add($"lethal option penalty -{tuning.RiskProfile.LethalOptionPenalty:F1}");
        }

        return total;
    }

    public double EvaluateMaxHpPenalty(int maxHpDelta, List<string> reasons, AiEventTuning tuning)
    {
        if (maxHpDelta >= 0)
        {
            return 0d;
        }

        double total = Math.Abs(maxHpDelta) * tuning.RiskProfile.MaxHpLossPenaltyPerPoint;
        reasons.Add($"maxHpPenalty={-total:F1}");
        return total;
    }

    public double EvaluateGoldDelta(int goldDelta, List<string> reasons, AiEventTuning tuning)
    {
        if (goldDelta == 0)
        {
            return 0d;
        }

        double total = goldDelta / tuning.OutcomeWeights.GoldValueDivisor;
        reasons.Add($"goldDeltaScore={(total >= 0 ? "+" : string.Empty)}{total:F1}");
        return total;
    }

    public double EvaluateCursePenalty(IEnumerable<string> curseIds, List<string> reasons, AiEventTuning tuning)
    {
        double total = 0d;
        foreach (string curseId in curseIds)
        {
            double penalty = curseId.ToUpperInvariant() switch
            {
                "REGRET" => 26d,
                "DOUBT" => 18d,
                "SHAME" => 18d,
                "WRITHE" => 20d,
                "PAIN" => 20d,
                "DECAY" => 22d,
                "DEBT" => 16d,
                "GUILTY" => 16d,
                _ => 14d
            } * tuning.RiskProfile.CursePenaltyMultiplier;
            total += penalty;
            reasons.Add($"curse={curseId} penalty={-penalty:F1}");
        }

        return total;
    }

    public double EvaluateRandomnessDiscount(EventOutcomeSummary outcome, List<string> reasons, AiEventTuning tuning)
    {
        if (!outcome.HasRandomness)
        {
            return 0d;
        }

        double discount = outcome.FixedCardCount > 0 || outcome.RelicIds.Count > 0 || outcome.PotionRewardCount > 0
            ? tuning.RiskProfile.RandomRewardDiscount
            : tuning.RiskProfile.RandomGenericDiscount;
        reasons.Add($"randomnessDiscount={-discount:F1}");
        return discount;
    }

    public double UnsupportedOptionPenalty(bool hasUnknownEffects, List<string> reasons, AiEventTuning tuning)
    {
        double penalty = hasUnknownEffects ? tuning.RiskProfile.UnknownEffectsPenalty : tuning.RiskProfile.UnsupportedPenalty;
        reasons.Add($"unsupportedPenalty={-penalty:F1}");
        return penalty;
    }

    private static double EvaluateUpgradeSpec(CardUpgradeSpec spec, List<string> reasons, AiEventTuning tuning)
    {
        double score = tuning.OutcomeWeights.UpgradeSpecBaseValue;
        if (spec.CostOverride.HasValue)
        {
            score += Math.Max(0, 2 - spec.CostOverride.Value) * tuning.OutcomeWeights.UpgradeCostOverrideValuePerEnergy;
            reasons.Add($"costOverride->{spec.CostOverride.Value}");
        }
        else if (spec.CostDelta < 0)
        {
            double bonus = Math.Abs(spec.CostDelta) * tuning.OutcomeWeights.UpgradeCostReductionValuePerEnergy;
            score += bonus;
            reasons.Add($"costReduction +{bonus:F1}");
        }

        foreach ((EffectAdjustmentKey _, int value) in spec.EffectAmountAdjustments)
        {
            score += Math.Max(0, value) * tuning.OutcomeWeights.UpgradePositiveEffectValuePerPoint;
        }

        if (spec.Retain == true)
        {
            score += tuning.OutcomeWeights.UpgradeRetainBonus;
            reasons.Add($"gains retain +{tuning.OutcomeWeights.UpgradeRetainBonus:F1}");
        }

        if (spec.Exhaust == false)
        {
            score += tuning.OutcomeWeights.UpgradeRemoveExhaustBonus;
            reasons.Add($"removes exhaust +{tuning.OutcomeWeights.UpgradeRemoveExhaustBonus:F1}");
        }

        if (spec.Ethereal == false)
        {
            score += tuning.OutcomeWeights.UpgradeRemoveEtherealBonus;
            reasons.Add($"removes ethereal +{tuning.OutcomeWeights.UpgradeRemoveEtherealBonus:F1}");
        }

        if (spec.ReplayCountOverride.HasValue && spec.ReplayCountOverride.Value > 1)
        {
            score += tuning.OutcomeWeights.UpgradeReplayIncreaseBonus;
            reasons.Add($"replay increase +{tuning.OutcomeWeights.UpgradeReplayIncreaseBonus:F1}");
        }

        reasons.Add($"upgradeSpecScore={score:F1}");
        return score;
    }

    private static double EvaluateCatalogCard(CardCatalogEntry entry)
    {
        double score = entry.Rarity switch
        {
            "Rare" => 18d,
            "Uncommon" => 14d,
            "Common" => 10d,
            "Basic" => 6d,
            "Curse" => -20d,
            _ => 8d
        };

        score += entry.Type switch
        {
            CardType.Power => 4d,
            CardType.Attack => 2d,
            CardType.Skill => 1d,
            _ => 0d
        };

        if (entry.BaseCost == 0)
        {
            score += 3d;
        }
        else if (entry.BaseCost >= 2)
        {
            score -= 2d;
        }

        score += entry.SemanticProfile.Effects.Sum(static effect =>
            effect.Kind switch
            {
                EffectKind.DealDamage => Math.Min(effect.Amount * effect.RepeatCount, 20) * 0.4d,
                EffectKind.GainBlock => Math.Min(effect.Amount * effect.RepeatCount, 20) * 0.35d,
                EffectKind.Summon => Math.Min(effect.Amount * effect.RepeatCount, 24) * 0.45d,
                EffectKind.DrawCards => effect.Amount * 4d,
                EffectKind.GainEnergy => effect.Amount * 6d,
                EffectKind.ApplyPower => effect.Amount * 1.2d,
                EffectKind.ChannelOrb => effect.Amount * 4d,
                EffectKind.EvokeOrb => effect.Amount * 4.5d,
                EffectKind.GenerateCards => effect.Amount * 5d,
                EffectKind.GeneratePotion => effect.Amount * 7d,
                EffectKind.DiscardCards => effect.Amount * 2.5d,
                EffectKind.ExhaustCards => effect.Amount * 2.5d,
                EffectKind.UpgradeCards => effect.Amount * 6d,
                EffectKind.RetainCards => effect.Amount * 3d,
                EffectKind.StatusBurden => -effect.Amount * 0.45d,
                EffectKind.SpecialUtility => effect.Amount * 0.8d,
                _ => 0.5d
            });

        return score;
    }

}
