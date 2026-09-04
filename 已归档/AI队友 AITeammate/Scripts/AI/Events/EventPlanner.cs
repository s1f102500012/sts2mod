using System;
using System.Collections.Generic;
using System.Linq;

namespace AITeammate.Scripts;

internal sealed class EventPlanner
{
    private readonly EventValuationHelpers _valuationHelpers = new();
    private readonly EventHandlerRegistry _handlerRegistry = new();

    public EventPlannerResult Evaluate(EventVisitState snapshot)
    {
        AiEventTuning tuning = AiCharacterCombatConfigLoader.LoadForPlayer(snapshot.Player).Events;
        IEventSpecialHandler? handler = _handlerRegistry.Resolve(snapshot);
        List<EventOptionEvaluation> evaluations = snapshot.Options
            .Select(option => EvaluateOption(snapshot, option, handler, tuning))
            .OrderByDescending(static evaluation => evaluation.TotalScore)
            .ThenBy(evaluation => evaluation.Title, StringComparer.Ordinal)
            .ToList();

        EventOptionEvaluation? baseline = evaluations
            .Where(static evaluation => evaluation.IsBaselineOption)
            .OrderByDescending(static evaluation => evaluation.TotalScore)
            .FirstOrDefault();
        EventOptionEvaluation best = TrySelectForcedOption(snapshot, evaluations) ??
                                     (evaluations.Count > 0
                                         ? evaluations[0]
                                         : BuildEmptyEvaluation());
        int supportedCount = evaluations.Count(static evaluation => evaluation.IsSupported);
        int fullyNormalizedCount = evaluations.Count(static evaluation => evaluation.IsFullyNormalized);
        int highTrustCount = evaluations.Count(static evaluation => evaluation.TrustLevel == EventPlannerTrustLevel.High);
        EventPlannerTrustLevel overallTrustLevel = DetermineOverallTrust(best, evaluations);

        return new EventPlannerResult
        {
            RankedOptions = evaluations,
            BestOption = best,
            BaselineOption = baseline,
            SupportedOptionCount = supportedCount,
            FullyNormalizedOptionCount = fullyNormalizedCount,
            HighTrustOptionCount = highTrustCount,
            IsBestOptionSafeForPlannerSelectionLater = best.IsSafeForPlannerSelectionLater,
            OverallTrustLevel = overallTrustLevel,
            CoverageSummary = $"handler={(handler?.HandlerName ?? "none")} supported={supportedCount}/{evaluations.Count} fullyNormalized={fullyNormalizedCount}/{evaluations.Count} highTrust={highTrustCount}/{evaluations.Count}"
        };
    }

    private EventOptionEvaluation EvaluateOption(EventVisitState snapshot, EventOptionDescriptor option, IEventSpecialHandler? handler, AiEventTuning tuning)
    {
        if (option.IsLocked)
        {
            return new EventOptionEvaluation
            {
                OptionIndex = option.OptionIndex,
                TextKey = option.TextKey,
                Title = option.Title,
                TotalScore = double.NegativeInfinity,
                IsBaselineOption = option.Outcome.LeaveLike || option.Outcome.ProceedLike,
                IsSupported = false,
                IsFullyNormalized = option.IsFullyNormalized,
                SupportLevel = option.SupportLevel,
                TrustLevel = option.TrustLevel,
                IsSafeForPlannerSelectionLater = false,
                Reasons = ["locked option"],
                Option = option
            };
        }

        EventOptionDescriptor normalized = handler?.Normalize(snapshot, option) ?? option;
        List<string> reasons = [];
        double score = 0d;

        reasons.Add($"handler={normalized.HandlerName}");
        reasons.Add($"support={normalized.SupportLevel}");
        reasons.Add($"trust={normalized.TrustLevel}");
        if (normalized.Outcome.LeaveLike || normalized.Outcome.ProceedLike)
        {
            reasons.Add("baseline leave/proceed score +0.0");
        }

        foreach (string relicId in normalized.Outcome.RelicIds)
        {
            string? rarity = snapshot.RuntimeEvent.CurrentOptions[normalized.OptionIndex].Relic?.Rarity.ToString();
            score += _valuationHelpers.EvaluateRelic(relicId, rarity, snapshot, reasons, tuning);
        }

        if (normalized.Outcome.PotionRewardCount > 0)
        {
            score += _valuationHelpers.EvaluatePotion(
                normalized.Outcome.PotionIds.FirstOrDefault(),
                null,
                normalized.Outcome.PotionRewardCount,
                snapshot,
                reasons,
                tuning);
        }

        if (normalized.Outcome.FixedCardCount > 0)
        {
            score += _valuationHelpers.EvaluateFixedCardGain(normalized.Outcome.FixedCardIds, snapshot, reasons, tuning);
        }

        if (normalized.Outcome.CardRewardCount > 0)
        {
            double rewardScore = normalized.Outcome.CardRewardCount * tuning.OutcomeWeights.CardRewardBaselinePerReward * tuning.OutcomeWeights.CardRewardMultiplier;
            score += rewardScore;
            reasons.Add($"cardRewardBaseline={rewardScore:F1}");
        }

        if (normalized.Outcome.RemoveCount > 0)
        {
            IReadOnlyList<EventRemovalCandidate> candidates = _valuationHelpers.SelectBestRemovalCandidates(snapshot, normalized.Outcome.RemoveCount);
            if (candidates.Count >= normalized.Outcome.RemoveCount)
            {
                double removalScore = candidates.Sum(static candidate => candidate.BurdenScore) * tuning.OutcomeWeights.RemovalRewardMultiplier;
                score += removalScore;
                reasons.Add($"removeTargets={string.Join(",", candidates.Select(static candidate => candidate.CardId))} removalScore={removalScore:F1}");
            }
            else
            {
                score -= 5000d;
                reasons.Add($"not enough safe removable card candidates candidates={candidates.Count} required={normalized.Outcome.RemoveCount}");
            }
        }

        if (normalized.Outcome.UpgradeCount > 0)
        {
            score += _valuationHelpers.EvaluateBestUpgradeTarget(snapshot, normalized.Outcome.UpgradeCount, reasons, tuning);
        }

        if (normalized.Outcome.TransformCount > 0)
        {
            score += _valuationHelpers.EvaluateTransform(snapshot, normalized.Outcome.TransformCount, reasons, tuning);
        }

        if (normalized.Outcome.EnchantCount > 0)
        {
            double enchantScore = tuning.OutcomeWeights.EnchantBaselinePerCard * normalized.Outcome.EnchantCount;
            score += enchantScore;
            reasons.Add($"enchantBaseline={enchantScore:F1}");
        }

        if (normalized.Outcome.MaxHpDelta > 0)
        {
            double maxHpGainScore = normalized.Outcome.MaxHpDelta * tuning.OutcomeWeights.MaxHpGainValuePerPoint;
            score += maxHpGainScore;
            reasons.Add($"maxHpGain={maxHpGainScore:F1}");
        }

        if (normalized.Outcome.HpDelta > 0)
        {
            double healScore = normalized.Outcome.HpDelta * tuning.OutcomeWeights.HealValuePerPoint;
            score += healScore;
            reasons.Add($"healScore={healScore:F1}");
        }

        score += _valuationHelpers.EvaluateGoldDelta(normalized.Outcome.GoldDelta, reasons, tuning);
        score -= _valuationHelpers.EvaluateHpPenalty(snapshot, Math.Max(0, -normalized.Outcome.HpDelta), normalized.WillKillPlayer, reasons, tuning);
        score -= _valuationHelpers.EvaluateMaxHpPenalty(normalized.Outcome.MaxHpDelta, reasons, tuning);
        score -= _valuationHelpers.EvaluateCursePenalty(normalized.Outcome.CurseCardIds, reasons, tuning);
        score -= _valuationHelpers.EvaluateRandomnessDiscount(normalized.Outcome, reasons, tuning);

        if (normalized.Outcome.StartsCombat)
        {
            score -= tuning.RiskProfile.StartsCombatPenalty;
            reasons.Add($"combat follow-up uncertainty -{tuning.RiskProfile.StartsCombatPenalty:F1}");
        }

        bool supported = normalized.SupportLevel != EventSupportLevel.Unsupported;
        if (!supported || normalized.Outcome.HasUnknownEffects)
        {
            score -= _valuationHelpers.UnsupportedOptionPenalty(normalized.Outcome.HasUnknownEffects, reasons, tuning);
        }

        foreach (string unknownReason in normalized.UnknownReasons)
        {
            reasons.Add($"unknown={unknownReason}");
        }

        return new EventOptionEvaluation
        {
            OptionIndex = normalized.OptionIndex,
            TextKey = normalized.TextKey,
            Title = normalized.Title,
            TotalScore = score,
            IsBaselineOption = normalized.Outcome.LeaveLike || normalized.Outcome.ProceedLike,
            IsSupported = supported,
            IsFullyNormalized = normalized.IsFullyNormalized,
            SupportLevel = normalized.SupportLevel,
            TrustLevel = normalized.TrustLevel,
            IsSafeForPlannerSelectionLater = normalized.IsSafeForPlannerSelectionLater,
            Reasons = reasons,
            Option = normalized
        };
    }

    private static EventOptionEvaluation? TrySelectForcedOption(EventVisitState snapshot, IReadOnlyList<EventOptionEvaluation> evaluations)
    {
        if (snapshot.EventTypeName == "SunkenStatue")
        {
            return evaluations.FirstOrDefault(static evaluation =>
                evaluation.OptionIndex == 1 &&
                evaluation.TrustLevel == EventPlannerTrustLevel.High &&
                evaluation.SupportLevel == EventSupportLevel.SpecialHighConfidence &&
                evaluation.TextKey.Contains(".DIVE_INTO_WATER", StringComparison.Ordinal));
        }

        if (snapshot.EventTypeName == "Orobas")
        {
            return evaluations.FirstOrDefault(static evaluation =>
                evaluation.TrustLevel == EventPlannerTrustLevel.High &&
                evaluation.SupportLevel == EventSupportLevel.SpecialHighConfidence &&
                (string.Equals(evaluation.Option.RelicId, "RADIANT_PEARL", StringComparison.Ordinal) ||
                 evaluation.TextKey.Contains(".RADIANT_PEARL", StringComparison.Ordinal)));
        }

        if (snapshot.EventTypeName == "PotionCourier")
        {
            return evaluations.FirstOrDefault(static evaluation =>
                evaluation.OptionIndex == 1 &&
                evaluation.TrustLevel == EventPlannerTrustLevel.High &&
                evaluation.SupportLevel == EventSupportLevel.SpecialHighConfidence);
        }

        return null;
    }

    private static EventOptionEvaluation BuildEmptyEvaluation()
    {
        EventOptionDescriptor emptyDescriptor = new()
        {
            OptionIndex = -1,
            TextKey = "none",
            Title = "No options",
            Description = string.Empty,
            IsLocked = true,
            IsProceed = false,
            IsLikelyLeaveOrExit = false,
            WillKillPlayer = false,
            IsFullyNormalized = false,
            NormalizationSource = "none",
            HandlerName = "none",
            SupportLevel = EventSupportLevel.Unsupported,
            TrustLevel = EventPlannerTrustLevel.Low,
            IsSafeForPlannerSelectionLater = false,
            RuntimeLocator = new EventRuntimeLocator
            {
                LocatorId = "none",
                OptionIndex = -1,
                TextKey = "none"
            },
            Outcome = new EventOutcomeSummary()
        };

        return new EventOptionEvaluation
        {
            OptionIndex = -1,
            TextKey = "none",
            Title = "No options",
            TotalScore = double.NegativeInfinity,
            IsBaselineOption = false,
            IsSupported = false,
            IsFullyNormalized = false,
            SupportLevel = EventSupportLevel.Unsupported,
            TrustLevel = EventPlannerTrustLevel.Low,
            IsSafeForPlannerSelectionLater = false,
            Reasons = ["no unlocked options"],
            Option = emptyDescriptor
        };
    }

    private static EventPlannerTrustLevel DetermineOverallTrust(EventOptionEvaluation best, IReadOnlyList<EventOptionEvaluation> evaluations)
    {
        if (evaluations.Count == 0)
        {
            return EventPlannerTrustLevel.Low;
        }

        if (best.TrustLevel == EventPlannerTrustLevel.High &&
            evaluations.Count(static evaluation => evaluation.TrustLevel == EventPlannerTrustLevel.High) >= 1)
        {
            return EventPlannerTrustLevel.High;
        }

        if (best.TrustLevel == EventPlannerTrustLevel.Medium ||
            evaluations.Any(static evaluation => evaluation.TrustLevel == EventPlannerTrustLevel.Medium))
        {
            return EventPlannerTrustLevel.Medium;
        }

        return EventPlannerTrustLevel.Low;
    }
}
