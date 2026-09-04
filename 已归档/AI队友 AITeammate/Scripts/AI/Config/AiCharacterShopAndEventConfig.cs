using System;

namespace AITeammate.Scripts;

internal sealed class AiShopTuning
{
    public required AiShopOfferPriorities OfferPriorities { get; init; }
    public required AiShopRelicWeights RelicWeights { get; init; }
    public required AiShopRemovalWeights RemovalWeights { get; init; }

    public static AiShopTuning CreateDefault()
    {
        return new AiShopTuning
        {
            OfferPriorities = AiShopOfferPriorities.CreateDefault(),
            RelicWeights = AiShopRelicWeights.CreateDefault(),
            RemovalWeights = AiShopRemovalWeights.CreateDefault()
        };
    }

    public AiShopTuning Merge(AiShopTuningOverrides? overrides)
    {
        return new AiShopTuning
        {
            OfferPriorities = OfferPriorities.Merge(overrides?.OfferPriorities),
            RelicWeights = RelicWeights.Merge(overrides?.RelicWeights),
            RemovalWeights = RemovalWeights.Merge(overrides?.RemovalWeights)
        };
    }
}

internal sealed class AiShopOfferPriorities
{
    public required double CardPurchaseBias { get; init; }
    public required double RelicPurchaseBias { get; init; }
    public required double PotionPurchaseBias { get; init; }
    public required double RemovalServiceBias { get; init; }
    public required double CardAboveThresholdBonus { get; init; }
    public required double CardBelowThresholdPenalty { get; init; }
    public required double SaleBonus { get; init; }
    public required double ColorlessPremiumPenalty { get; init; }
    public required double GoldReserveValuePerGold { get; init; }

    public static AiShopOfferPriorities CreateDefault()
    {
        return new AiShopOfferPriorities
        {
            CardPurchaseBias = 0d,
            RelicPurchaseBias = 0d,
            PotionPurchaseBias = 0d,
            RemovalServiceBias = 0d,
            CardAboveThresholdBonus = 4d,
            CardBelowThresholdPenalty = 4d,
            SaleBonus = 2.5d,
            ColorlessPremiumPenalty = 1.5d,
            GoldReserveValuePerGold = 0d
        };
    }

    public AiShopOfferPriorities Merge(AiShopOfferPrioritiesOverrides? overrides)
    {
        return new AiShopOfferPriorities
        {
            CardPurchaseBias = AiCombatCoreWeights.ClampDouble(overrides?.CardPurchaseBias, CardPurchaseBias, -40d, 40d),
            RelicPurchaseBias = AiCombatCoreWeights.ClampDouble(overrides?.RelicPurchaseBias, RelicPurchaseBias, -40d, 40d),
            PotionPurchaseBias = AiCombatCoreWeights.ClampDouble(overrides?.PotionPurchaseBias, PotionPurchaseBias, -40d, 40d),
            RemovalServiceBias = AiCombatCoreWeights.ClampDouble(overrides?.RemovalServiceBias, RemovalServiceBias, -40d, 40d),
            CardAboveThresholdBonus = AiCombatCoreWeights.ClampDouble(overrides?.CardAboveThresholdBonus, CardAboveThresholdBonus, -20d, 40d),
            CardBelowThresholdPenalty = AiCombatCoreWeights.ClampDouble(overrides?.CardBelowThresholdPenalty, CardBelowThresholdPenalty, -20d, 40d),
            SaleBonus = AiCombatCoreWeights.ClampDouble(overrides?.SaleBonus, SaleBonus, -20d, 40d),
            ColorlessPremiumPenalty = AiCombatCoreWeights.ClampDouble(overrides?.ColorlessPremiumPenalty, ColorlessPremiumPenalty, -20d, 40d),
            GoldReserveValuePerGold = AiCombatCoreWeights.ClampDouble(overrides?.GoldReserveValuePerGold, GoldReserveValuePerGold, -1d, 1d)
        };
    }
}

internal sealed class AiShopRelicWeights
{
    public required double AncientBaseline { get; init; }
    public required double RareBaseline { get; init; }
    public required double UncommonBaseline { get; init; }
    public required double CommonBaseline { get; init; }
    public required double FallbackBaseline { get; init; }
    public required double CostDivisor { get; init; }
    public required double SpecialRelicBonusMultiplier { get; init; }
    public required double StrikeDummyBaseBonus { get; init; }
    public required double StrikeDummyBonusPerStrike { get; init; }
    public required double DuplicateMembershipPenalty { get; init; }
    public required double DuplicateCourierPenalty { get; init; }

    public static AiShopRelicWeights CreateDefault()
    {
        return new AiShopRelicWeights
        {
            AncientBaseline = 28d,
            RareBaseline = 21d,
            UncommonBaseline = 15d,
            CommonBaseline = 10d,
            FallbackBaseline = 8d,
            CostDivisor = 12d,
            SpecialRelicBonusMultiplier = 1d,
            StrikeDummyBaseBonus = 3d,
            StrikeDummyBonusPerStrike = 1d,
            DuplicateMembershipPenalty = 20d,
            DuplicateCourierPenalty = 18d
        };
    }

    public AiShopRelicWeights Merge(AiShopRelicWeightsOverrides? overrides)
    {
        return new AiShopRelicWeights
        {
            AncientBaseline = AiCombatCoreWeights.ClampDouble(overrides?.AncientBaseline, AncientBaseline, -20d, 80d),
            RareBaseline = AiCombatCoreWeights.ClampDouble(overrides?.RareBaseline, RareBaseline, -20d, 80d),
            UncommonBaseline = AiCombatCoreWeights.ClampDouble(overrides?.UncommonBaseline, UncommonBaseline, -20d, 80d),
            CommonBaseline = AiCombatCoreWeights.ClampDouble(overrides?.CommonBaseline, CommonBaseline, -20d, 80d),
            FallbackBaseline = AiCombatCoreWeights.ClampDouble(overrides?.FallbackBaseline, FallbackBaseline, -20d, 80d),
            CostDivisor = AiCombatCoreWeights.ClampDouble(overrides?.CostDivisor, CostDivisor, 1d, 60d),
            SpecialRelicBonusMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.SpecialRelicBonusMultiplier, SpecialRelicBonusMultiplier, 0d, 4d),
            StrikeDummyBaseBonus = AiCombatCoreWeights.ClampDouble(overrides?.StrikeDummyBaseBonus, StrikeDummyBaseBonus, -20d, 40d),
            StrikeDummyBonusPerStrike = AiCombatCoreWeights.ClampDouble(overrides?.StrikeDummyBonusPerStrike, StrikeDummyBonusPerStrike, -10d, 20d),
            DuplicateMembershipPenalty = AiCombatCoreWeights.ClampDouble(overrides?.DuplicateMembershipPenalty, DuplicateMembershipPenalty, 0d, 100d),
            DuplicateCourierPenalty = AiCombatCoreWeights.ClampDouble(overrides?.DuplicateCourierPenalty, DuplicateCourierPenalty, 0d, 100d)
        };
    }
}

internal sealed class AiShopRemovalWeights
{
    public required double BurdenMultiplier { get; init; }
    public required double SmallDeckBonus { get; init; }
    public required double MediumDeckBonus { get; init; }
    public required double LargeDeckBonus { get; init; }
    public required double BasicCardBonusPerCard { get; init; }
    public required double HeavyCurveConsistencyBonus { get; init; }
    public required double NoZeroCostConsistencyBonus { get; init; }
    public required double CostDivisor { get; init; }

    public static AiShopRemovalWeights CreateDefault()
    {
        return new AiShopRemovalWeights
        {
            BurdenMultiplier = 1d,
            SmallDeckBonus = 2d,
            MediumDeckBonus = 4d,
            LargeDeckBonus = 7d,
            BasicCardBonusPerCard = 1.5d,
            HeavyCurveConsistencyBonus = 2.5d,
            NoZeroCostConsistencyBonus = 1d,
            CostDivisor = 8.5d
        };
    }

    public AiShopRemovalWeights Merge(AiShopRemovalWeightsOverrides? overrides)
    {
        return new AiShopRemovalWeights
        {
            BurdenMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.BurdenMultiplier, BurdenMultiplier, 0d, 5d),
            SmallDeckBonus = AiCombatCoreWeights.ClampDouble(overrides?.SmallDeckBonus, SmallDeckBonus, -20d, 30d),
            MediumDeckBonus = AiCombatCoreWeights.ClampDouble(overrides?.MediumDeckBonus, MediumDeckBonus, -20d, 30d),
            LargeDeckBonus = AiCombatCoreWeights.ClampDouble(overrides?.LargeDeckBonus, LargeDeckBonus, -20d, 40d),
            BasicCardBonusPerCard = AiCombatCoreWeights.ClampDouble(overrides?.BasicCardBonusPerCard, BasicCardBonusPerCard, -10d, 10d),
            HeavyCurveConsistencyBonus = AiCombatCoreWeights.ClampDouble(overrides?.HeavyCurveConsistencyBonus, HeavyCurveConsistencyBonus, -20d, 20d),
            NoZeroCostConsistencyBonus = AiCombatCoreWeights.ClampDouble(overrides?.NoZeroCostConsistencyBonus, NoZeroCostConsistencyBonus, -20d, 20d),
            CostDivisor = AiCombatCoreWeights.ClampDouble(overrides?.CostDivisor, CostDivisor, 1d, 60d)
        };
    }
}

internal sealed class AiEventTuning
{
    public required AiEventOutcomeWeights OutcomeWeights { get; init; }
    public required AiEventRelicWeights RelicWeights { get; init; }
    public required AiEventRiskProfile RiskProfile { get; init; }

    public static AiEventTuning CreateDefault()
    {
        return new AiEventTuning
        {
            OutcomeWeights = AiEventOutcomeWeights.CreateDefault(),
            RelicWeights = AiEventRelicWeights.CreateDefault(),
            RiskProfile = AiEventRiskProfile.CreateDefault()
        };
    }

    public AiEventTuning Merge(AiEventTuningOverrides? overrides)
    {
        return new AiEventTuning
        {
            OutcomeWeights = OutcomeWeights.Merge(overrides?.OutcomeWeights),
            RelicWeights = RelicWeights.Merge(overrides?.RelicWeights),
            RiskProfile = RiskProfile.Merge(overrides?.RiskProfile)
        };
    }
}

internal sealed class AiEventOutcomeWeights
{
    public required double RelicRewardMultiplier { get; init; }
    public required double PotionRewardMultiplier { get; init; }
    public required double FixedCardRewardMultiplier { get; init; }
    public required double CardRewardBaselinePerReward { get; init; }
    public required double CardRewardMultiplier { get; init; }
    public required double RemovalRewardMultiplier { get; init; }
    public required double UpgradeRewardMultiplier { get; init; }
    public required double TransformRewardMultiplier { get; init; }
    public required double TransformRemovalValueMultiplier { get; init; }
    public required double TransformReplacementBaselinePerCard { get; init; }
    public required double EnchantBaselinePerCard { get; init; }
    public required double MaxHpGainValuePerPoint { get; init; }
    public required double HealValuePerPoint { get; init; }
    public required double GoldValueDivisor { get; init; }
    public required double UpgradeSpecBaseValue { get; init; }
    public required double UpgradeCostOverrideValuePerEnergy { get; init; }
    public required double UpgradeCostReductionValuePerEnergy { get; init; }
    public required double UpgradePositiveEffectValuePerPoint { get; init; }
    public required double UpgradeRetainBonus { get; init; }
    public required double UpgradeRemoveExhaustBonus { get; init; }
    public required double UpgradeRemoveEtherealBonus { get; init; }
    public required double UpgradeReplayIncreaseBonus { get; init; }
    public required double UpgradeBasicCardBonus { get; init; }
    public required double UpgradePowerCardBonus { get; init; }

    public static AiEventOutcomeWeights CreateDefault()
    {
        return new AiEventOutcomeWeights
        {
            RelicRewardMultiplier = 1d,
            PotionRewardMultiplier = 1d,
            FixedCardRewardMultiplier = 1d,
            CardRewardBaselinePerReward = 12d,
            CardRewardMultiplier = 1d,
            RemovalRewardMultiplier = 1d,
            UpgradeRewardMultiplier = 1d,
            TransformRewardMultiplier = 1d,
            TransformRemovalValueMultiplier = 0.65d,
            TransformReplacementBaselinePerCard = 11d,
            EnchantBaselinePerCard = 10d,
            MaxHpGainValuePerPoint = 3.5d,
            HealValuePerPoint = 1.8d,
            GoldValueDivisor = 12d,
            UpgradeSpecBaseValue = 4d,
            UpgradeCostOverrideValuePerEnergy = 4d,
            UpgradeCostReductionValuePerEnergy = 5d,
            UpgradePositiveEffectValuePerPoint = 1.5d,
            UpgradeRetainBonus = 3d,
            UpgradeRemoveExhaustBonus = 2d,
            UpgradeRemoveEtherealBonus = 3d,
            UpgradeReplayIncreaseBonus = 4d,
            UpgradeBasicCardBonus = 2d,
            UpgradePowerCardBonus = 2d
        };
    }

    public AiEventOutcomeWeights Merge(AiEventOutcomeWeightsOverrides? overrides)
    {
        return new AiEventOutcomeWeights
        {
            RelicRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.RelicRewardMultiplier, RelicRewardMultiplier, 0d, 5d),
            PotionRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.PotionRewardMultiplier, PotionRewardMultiplier, 0d, 5d),
            FixedCardRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.FixedCardRewardMultiplier, FixedCardRewardMultiplier, 0d, 5d),
            CardRewardBaselinePerReward = AiCombatCoreWeights.ClampDouble(overrides?.CardRewardBaselinePerReward, CardRewardBaselinePerReward, -20d, 60d),
            CardRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.CardRewardMultiplier, CardRewardMultiplier, 0d, 5d),
            RemovalRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.RemovalRewardMultiplier, RemovalRewardMultiplier, 0d, 5d),
            UpgradeRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeRewardMultiplier, UpgradeRewardMultiplier, 0d, 5d),
            TransformRewardMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.TransformRewardMultiplier, TransformRewardMultiplier, 0d, 5d),
            TransformRemovalValueMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.TransformRemovalValueMultiplier, TransformRemovalValueMultiplier, 0d, 5d),
            TransformReplacementBaselinePerCard = AiCombatCoreWeights.ClampDouble(overrides?.TransformReplacementBaselinePerCard, TransformReplacementBaselinePerCard, -20d, 60d),
            EnchantBaselinePerCard = AiCombatCoreWeights.ClampDouble(overrides?.EnchantBaselinePerCard, EnchantBaselinePerCard, -20d, 40d),
            MaxHpGainValuePerPoint = AiCombatCoreWeights.ClampDouble(overrides?.MaxHpGainValuePerPoint, MaxHpGainValuePerPoint, -10d, 20d),
            HealValuePerPoint = AiCombatCoreWeights.ClampDouble(overrides?.HealValuePerPoint, HealValuePerPoint, -10d, 20d),
            GoldValueDivisor = AiCombatCoreWeights.ClampDouble(overrides?.GoldValueDivisor, GoldValueDivisor, 1d, 60d),
            UpgradeSpecBaseValue = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeSpecBaseValue, UpgradeSpecBaseValue, -20d, 30d),
            UpgradeCostOverrideValuePerEnergy = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeCostOverrideValuePerEnergy, UpgradeCostOverrideValuePerEnergy, 0d, 20d),
            UpgradeCostReductionValuePerEnergy = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeCostReductionValuePerEnergy, UpgradeCostReductionValuePerEnergy, 0d, 20d),
            UpgradePositiveEffectValuePerPoint = AiCombatCoreWeights.ClampDouble(overrides?.UpgradePositiveEffectValuePerPoint, UpgradePositiveEffectValuePerPoint, 0d, 10d),
            UpgradeRetainBonus = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeRetainBonus, UpgradeRetainBonus, -20d, 20d),
            UpgradeRemoveExhaustBonus = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeRemoveExhaustBonus, UpgradeRemoveExhaustBonus, -20d, 20d),
            UpgradeRemoveEtherealBonus = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeRemoveEtherealBonus, UpgradeRemoveEtherealBonus, -20d, 20d),
            UpgradeReplayIncreaseBonus = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeReplayIncreaseBonus, UpgradeReplayIncreaseBonus, -20d, 20d),
            UpgradeBasicCardBonus = AiCombatCoreWeights.ClampDouble(overrides?.UpgradeBasicCardBonus, UpgradeBasicCardBonus, -20d, 20d),
            UpgradePowerCardBonus = AiCombatCoreWeights.ClampDouble(overrides?.UpgradePowerCardBonus, UpgradePowerCardBonus, -20d, 20d)
        };
    }
}

internal sealed class AiEventRelicWeights
{
    public required double AncientBaseline { get; init; }
    public required double RareBaseline { get; init; }
    public required double UncommonBaseline { get; init; }
    public required double CommonBaseline { get; init; }
    public required double FallbackBaseline { get; init; }
    public required double SpecialRelicBonusMultiplier { get; init; }
    public required double DuplicateOwnedPenalty { get; init; }

    public static AiEventRelicWeights CreateDefault()
    {
        return new AiEventRelicWeights
        {
            AncientBaseline = 28d,
            RareBaseline = 21d,
            UncommonBaseline = 15d,
            CommonBaseline = 10d,
            FallbackBaseline = 8d,
            SpecialRelicBonusMultiplier = 1d,
            DuplicateOwnedPenalty = 20d
        };
    }

    public AiEventRelicWeights Merge(AiEventRelicWeightsOverrides? overrides)
    {
        return new AiEventRelicWeights
        {
            AncientBaseline = AiCombatCoreWeights.ClampDouble(overrides?.AncientBaseline, AncientBaseline, -20d, 80d),
            RareBaseline = AiCombatCoreWeights.ClampDouble(overrides?.RareBaseline, RareBaseline, -20d, 80d),
            UncommonBaseline = AiCombatCoreWeights.ClampDouble(overrides?.UncommonBaseline, UncommonBaseline, -20d, 80d),
            CommonBaseline = AiCombatCoreWeights.ClampDouble(overrides?.CommonBaseline, CommonBaseline, -20d, 80d),
            FallbackBaseline = AiCombatCoreWeights.ClampDouble(overrides?.FallbackBaseline, FallbackBaseline, -20d, 80d),
            SpecialRelicBonusMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.SpecialRelicBonusMultiplier, SpecialRelicBonusMultiplier, 0d, 4d),
            DuplicateOwnedPenalty = AiCombatCoreWeights.ClampDouble(overrides?.DuplicateOwnedPenalty, DuplicateOwnedPenalty, 0d, 100d)
        };
    }
}

internal sealed class AiEventRiskProfile
{
    public required double HpPenaltyCriticalPerPoint { get; init; }
    public required double HpPenaltyLowPerPoint { get; init; }
    public required double HpPenaltyMidPerPoint { get; init; }
    public required double HpPenaltyHealthyPerPoint { get; init; }
    public required double MaxHpLossPenaltyPerPoint { get; init; }
    public required double CursePenaltyMultiplier { get; init; }
    public required double RandomRewardDiscount { get; init; }
    public required double RandomGenericDiscount { get; init; }
    public required double StartsCombatPenalty { get; init; }
    public required double UnsupportedPenalty { get; init; }
    public required double UnknownEffectsPenalty { get; init; }
    public required double LethalOptionPenalty { get; init; }

    public static AiEventRiskProfile CreateDefault()
    {
        return new AiEventRiskProfile
        {
            HpPenaltyCriticalPerPoint = 4.8d,
            HpPenaltyLowPerPoint = 3.8d,
            HpPenaltyMidPerPoint = 2.9d,
            HpPenaltyHealthyPerPoint = 2.0d,
            MaxHpLossPenaltyPerPoint = 4.5d,
            CursePenaltyMultiplier = 1d,
            RandomRewardDiscount = 6d,
            RandomGenericDiscount = 10d,
            StartsCombatPenalty = 8d,
            UnsupportedPenalty = 6d,
            UnknownEffectsPenalty = 12d,
            LethalOptionPenalty = 1000d
        };
    }

    public AiEventRiskProfile Merge(AiEventRiskProfileOverrides? overrides)
    {
        return new AiEventRiskProfile
        {
            HpPenaltyCriticalPerPoint = AiCombatCoreWeights.ClampDouble(overrides?.HpPenaltyCriticalPerPoint, HpPenaltyCriticalPerPoint, 0d, 20d),
            HpPenaltyLowPerPoint = AiCombatCoreWeights.ClampDouble(overrides?.HpPenaltyLowPerPoint, HpPenaltyLowPerPoint, 0d, 20d),
            HpPenaltyMidPerPoint = AiCombatCoreWeights.ClampDouble(overrides?.HpPenaltyMidPerPoint, HpPenaltyMidPerPoint, 0d, 20d),
            HpPenaltyHealthyPerPoint = AiCombatCoreWeights.ClampDouble(overrides?.HpPenaltyHealthyPerPoint, HpPenaltyHealthyPerPoint, 0d, 20d),
            MaxHpLossPenaltyPerPoint = AiCombatCoreWeights.ClampDouble(overrides?.MaxHpLossPenaltyPerPoint, MaxHpLossPenaltyPerPoint, 0d, 30d),
            CursePenaltyMultiplier = AiCombatCoreWeights.ClampDouble(overrides?.CursePenaltyMultiplier, CursePenaltyMultiplier, 0d, 5d),
            RandomRewardDiscount = AiCombatCoreWeights.ClampDouble(overrides?.RandomRewardDiscount, RandomRewardDiscount, 0d, 40d),
            RandomGenericDiscount = AiCombatCoreWeights.ClampDouble(overrides?.RandomGenericDiscount, RandomGenericDiscount, 0d, 40d),
            StartsCombatPenalty = AiCombatCoreWeights.ClampDouble(overrides?.StartsCombatPenalty, StartsCombatPenalty, -20d, 60d),
            UnsupportedPenalty = AiCombatCoreWeights.ClampDouble(overrides?.UnsupportedPenalty, UnsupportedPenalty, 0d, 40d),
            UnknownEffectsPenalty = AiCombatCoreWeights.ClampDouble(overrides?.UnknownEffectsPenalty, UnknownEffectsPenalty, 0d, 60d),
            LethalOptionPenalty = AiCombatCoreWeights.ClampDouble(overrides?.LethalOptionPenalty, LethalOptionPenalty, 0d, 5000d)
        };
    }
}

internal sealed class AiShopTuningOverrides
{
    public AiShopOfferPrioritiesOverrides? OfferPriorities { get; set; }
    public AiShopRelicWeightsOverrides? RelicWeights { get; set; }
    public AiShopRemovalWeightsOverrides? RemovalWeights { get; set; }
}

internal sealed class AiShopOfferPrioritiesOverrides
{
    public double? CardPurchaseBias { get; set; }
    public double? RelicPurchaseBias { get; set; }
    public double? PotionPurchaseBias { get; set; }
    public double? RemovalServiceBias { get; set; }
    public double? CardAboveThresholdBonus { get; set; }
    public double? CardBelowThresholdPenalty { get; set; }
    public double? SaleBonus { get; set; }
    public double? ColorlessPremiumPenalty { get; set; }
    public double? GoldReserveValuePerGold { get; set; }
}

internal sealed class AiShopRelicWeightsOverrides
{
    public double? AncientBaseline { get; set; }
    public double? RareBaseline { get; set; }
    public double? UncommonBaseline { get; set; }
    public double? CommonBaseline { get; set; }
    public double? FallbackBaseline { get; set; }
    public double? CostDivisor { get; set; }
    public double? SpecialRelicBonusMultiplier { get; set; }
    public double? StrikeDummyBaseBonus { get; set; }
    public double? StrikeDummyBonusPerStrike { get; set; }
    public double? DuplicateMembershipPenalty { get; set; }
    public double? DuplicateCourierPenalty { get; set; }
}

internal sealed class AiShopRemovalWeightsOverrides
{
    public double? BurdenMultiplier { get; set; }
    public double? SmallDeckBonus { get; set; }
    public double? MediumDeckBonus { get; set; }
    public double? LargeDeckBonus { get; set; }
    public double? BasicCardBonusPerCard { get; set; }
    public double? HeavyCurveConsistencyBonus { get; set; }
    public double? NoZeroCostConsistencyBonus { get; set; }
    public double? CostDivisor { get; set; }
}

internal sealed class AiEventTuningOverrides
{
    public AiEventOutcomeWeightsOverrides? OutcomeWeights { get; set; }
    public AiEventRelicWeightsOverrides? RelicWeights { get; set; }
    public AiEventRiskProfileOverrides? RiskProfile { get; set; }
}

internal sealed class AiEventOutcomeWeightsOverrides
{
    public double? RelicRewardMultiplier { get; set; }
    public double? PotionRewardMultiplier { get; set; }
    public double? FixedCardRewardMultiplier { get; set; }
    public double? CardRewardBaselinePerReward { get; set; }
    public double? CardRewardMultiplier { get; set; }
    public double? RemovalRewardMultiplier { get; set; }
    public double? UpgradeRewardMultiplier { get; set; }
    public double? TransformRewardMultiplier { get; set; }
    public double? TransformRemovalValueMultiplier { get; set; }
    public double? TransformReplacementBaselinePerCard { get; set; }
    public double? EnchantBaselinePerCard { get; set; }
    public double? MaxHpGainValuePerPoint { get; set; }
    public double? HealValuePerPoint { get; set; }
    public double? GoldValueDivisor { get; set; }
    public double? UpgradeSpecBaseValue { get; set; }
    public double? UpgradeCostOverrideValuePerEnergy { get; set; }
    public double? UpgradeCostReductionValuePerEnergy { get; set; }
    public double? UpgradePositiveEffectValuePerPoint { get; set; }
    public double? UpgradeRetainBonus { get; set; }
    public double? UpgradeRemoveExhaustBonus { get; set; }
    public double? UpgradeRemoveEtherealBonus { get; set; }
    public double? UpgradeReplayIncreaseBonus { get; set; }
    public double? UpgradeBasicCardBonus { get; set; }
    public double? UpgradePowerCardBonus { get; set; }
}

internal sealed class AiEventRelicWeightsOverrides
{
    public double? AncientBaseline { get; set; }
    public double? RareBaseline { get; set; }
    public double? UncommonBaseline { get; set; }
    public double? CommonBaseline { get; set; }
    public double? FallbackBaseline { get; set; }
    public double? SpecialRelicBonusMultiplier { get; set; }
    public double? DuplicateOwnedPenalty { get; set; }
}

internal sealed class AiEventRiskProfileOverrides
{
    public double? HpPenaltyCriticalPerPoint { get; set; }
    public double? HpPenaltyLowPerPoint { get; set; }
    public double? HpPenaltyMidPerPoint { get; set; }
    public double? HpPenaltyHealthyPerPoint { get; set; }
    public double? MaxHpLossPenaltyPerPoint { get; set; }
    public double? CursePenaltyMultiplier { get; set; }
    public double? RandomRewardDiscount { get; set; }
    public double? RandomGenericDiscount { get; set; }
    public double? StartsCombatPenalty { get; set; }
    public double? UnsupportedPenalty { get; set; }
    public double? UnknownEffectsPenalty { get; set; }
    public double? LethalOptionPenalty { get; set; }
}
