using System;

namespace AITeammate.Scripts;

internal sealed class AiCardRewardTuning
{
    public required AiCardRewardIntrinsicWeights IntrinsicWeights { get; init; }
    public required AiCardRewardSynergyWeights SynergyWeights { get; init; }
    public required AiCardRewardDisciplineWeights DisciplineWeights { get; init; }

    public static AiCardRewardTuning CreateDefault()
    {
        return new AiCardRewardTuning
        {
            IntrinsicWeights = AiCardRewardIntrinsicWeights.CreateDefault(),
            SynergyWeights = AiCardRewardSynergyWeights.CreateDefault(),
            DisciplineWeights = AiCardRewardDisciplineWeights.CreateDefault()
        };
    }

    public AiCardRewardTuning Merge(AiCardRewardTuningOverrides? overrides)
    {
        return new AiCardRewardTuning
        {
            IntrinsicWeights = IntrinsicWeights.Merge(overrides?.IntrinsicWeights),
            SynergyWeights = SynergyWeights.Merge(overrides?.SynergyWeights),
            DisciplineWeights = DisciplineWeights.Merge(overrides?.DisciplineWeights)
        };
    }
}

internal sealed class AiCardRewardIntrinsicWeights
{
    public required double DamageValuePerPoint { get; init; }
    public required double BlockValuePerPoint { get; init; }
    public required double DrawValue { get; init; }
    public required double EnergyValue { get; init; }
    public required double VulnerableValue { get; init; }
    public required double WeakValue { get; init; }
    public required double PersistentStrengthValue { get; init; }
    public required double PersistentDexterityValue { get; init; }
    public required double TemporaryStrengthValue { get; init; }
    public required double TemporaryDexterityValue { get; init; }
    public required double RepeatValue { get; init; }
    public required double PowerBonus { get; init; }
    public required double ZeroCostBonus { get; init; }
    public required double HighCostPenaltyPerExtraEnergy { get; init; }
    public required double RetainBonus { get; init; }
    public required double GoodExhaustBonus { get; init; }
    public required double BadExhaustPenalty { get; init; }
    public required double EtherealPenalty { get; init; }
    public required double UnknownValuePenalty { get; init; }
    public required double RareBonus { get; init; }
    public required double UncommonBonus { get; init; }
    public required double BasicBonus { get; init; }
    public required double CursePenalty { get; init; }
    public required double StatusPenalty { get; init; }
    public required double QuestPenalty { get; init; }
    public required double EventBonus { get; init; }
    public required double AncientBonus { get; init; }

    public static AiCardRewardIntrinsicWeights CreateDefault()
    {
        return new AiCardRewardIntrinsicWeights
        {
            DamageValuePerPoint = 0.55d,
            BlockValuePerPoint = 0.45d,
            DrawValue = 8d,
            EnergyValue = 12d,
            VulnerableValue = 5d,
            WeakValue = 5.5d,
            PersistentStrengthValue = 6d,
            PersistentDexterityValue = 6d,
            TemporaryStrengthValue = 4d,
            TemporaryDexterityValue = 4d,
            RepeatValue = 0.75d,
            PowerBonus = 2d,
            ZeroCostBonus = 4d,
            HighCostPenaltyPerExtraEnergy = 2.25d,
            RetainBonus = 2d,
            GoodExhaustBonus = 1d,
            BadExhaustPenalty = 2d,
            EtherealPenalty = 6d,
            UnknownValuePenalty = 6d,
            RareBonus = 6d,
            UncommonBonus = 3d,
            BasicBonus = -3d,
            CursePenalty = -35d,
            StatusPenalty = -25d,
            QuestPenalty = -10d,
            EventBonus = 1d,
            AncientBonus = 8d
        };
    }

    public AiCardRewardIntrinsicWeights Merge(AiCardRewardIntrinsicWeightsOverrides? overrides)
    {
        return new AiCardRewardIntrinsicWeights
        {
            DamageValuePerPoint = AiCombatCoreWeights.ClampDouble(overrides?.DamageValuePerPoint, DamageValuePerPoint, -5d, 20d),
            BlockValuePerPoint = AiCombatCoreWeights.ClampDouble(overrides?.BlockValuePerPoint, BlockValuePerPoint, -5d, 20d),
            DrawValue = AiCombatCoreWeights.ClampDouble(overrides?.DrawValue, DrawValue, -20d, 40d),
            EnergyValue = AiCombatCoreWeights.ClampDouble(overrides?.EnergyValue, EnergyValue, -20d, 50d),
            VulnerableValue = AiCombatCoreWeights.ClampDouble(overrides?.VulnerableValue, VulnerableValue, -20d, 30d),
            WeakValue = AiCombatCoreWeights.ClampDouble(overrides?.WeakValue, WeakValue, -20d, 30d),
            PersistentStrengthValue = AiCombatCoreWeights.ClampDouble(overrides?.PersistentStrengthValue, PersistentStrengthValue, -20d, 40d),
            PersistentDexterityValue = AiCombatCoreWeights.ClampDouble(overrides?.PersistentDexterityValue, PersistentDexterityValue, -20d, 40d),
            TemporaryStrengthValue = AiCombatCoreWeights.ClampDouble(overrides?.TemporaryStrengthValue, TemporaryStrengthValue, -20d, 30d),
            TemporaryDexterityValue = AiCombatCoreWeights.ClampDouble(overrides?.TemporaryDexterityValue, TemporaryDexterityValue, -20d, 30d),
            RepeatValue = AiCombatCoreWeights.ClampDouble(overrides?.RepeatValue, RepeatValue, -5d, 10d),
            PowerBonus = AiCombatCoreWeights.ClampDouble(overrides?.PowerBonus, PowerBonus, -20d, 20d),
            ZeroCostBonus = AiCombatCoreWeights.ClampDouble(overrides?.ZeroCostBonus, ZeroCostBonus, -20d, 20d),
            HighCostPenaltyPerExtraEnergy = AiCombatCoreWeights.ClampDouble(overrides?.HighCostPenaltyPerExtraEnergy, HighCostPenaltyPerExtraEnergy, 0d, 20d),
            RetainBonus = AiCombatCoreWeights.ClampDouble(overrides?.RetainBonus, RetainBonus, -20d, 20d),
            GoodExhaustBonus = AiCombatCoreWeights.ClampDouble(overrides?.GoodExhaustBonus, GoodExhaustBonus, -20d, 20d),
            BadExhaustPenalty = AiCombatCoreWeights.ClampDouble(overrides?.BadExhaustPenalty, BadExhaustPenalty, 0d, 30d),
            EtherealPenalty = AiCombatCoreWeights.ClampDouble(overrides?.EtherealPenalty, EtherealPenalty, 0d, 30d),
            UnknownValuePenalty = AiCombatCoreWeights.ClampDouble(overrides?.UnknownValuePenalty, UnknownValuePenalty, 0d, 30d),
            RareBonus = AiCombatCoreWeights.ClampDouble(overrides?.RareBonus, RareBonus, -30d, 30d),
            UncommonBonus = AiCombatCoreWeights.ClampDouble(overrides?.UncommonBonus, UncommonBonus, -30d, 30d),
            BasicBonus = AiCombatCoreWeights.ClampDouble(overrides?.BasicBonus, BasicBonus, -30d, 30d),
            CursePenalty = AiCombatCoreWeights.ClampDouble(overrides?.CursePenalty, CursePenalty, -100d, 0d),
            StatusPenalty = AiCombatCoreWeights.ClampDouble(overrides?.StatusPenalty, StatusPenalty, -100d, 0d),
            QuestPenalty = AiCombatCoreWeights.ClampDouble(overrides?.QuestPenalty, QuestPenalty, -50d, 20d),
            EventBonus = AiCombatCoreWeights.ClampDouble(overrides?.EventBonus, EventBonus, -20d, 20d),
            AncientBonus = AiCombatCoreWeights.ClampDouble(overrides?.AncientBonus, AncientBonus, -30d, 40d)
        };
    }
}

internal sealed class AiCardRewardSynergyWeights
{
    public required double DrawWithHighCurveValue { get; init; }
    public required double EnergyWithHeavyCurveValue { get; init; }
    public required double AttackScalingSynergyPerAttack { get; init; }
    public required double DefenseScalingSynergyPerBlockSource { get; init; }
    public required double PowerWithDrawBonus { get; init; }
    public required double RetainWithHighCostBonus { get; init; }
    public required double ExhaustSynergyBonus { get; init; }
    public required double DamageNeedScale { get; init; }
    public required double DamageNeedCap { get; init; }
    public required double VulnerableNeedValue { get; init; }
    public required double BlockNeedScale { get; init; }
    public required double BlockNeedCap { get; init; }
    public required double WeakNeedValue { get; init; }
    public required double DexterityNeedValue { get; init; }
    public required double DrawNeedValue { get; init; }
    public required double EnergyNeedValue { get; init; }
    public required double ScalingNeedValue { get; init; }
    public required double PowerScalingBonus { get; init; }
    public required double EarlyCheapCardTempoScale { get; init; }
    public required double EarlyCheapCardTempoCap { get; init; }
    public required double EarlyActTempoScale { get; init; }
    public required double EarlyActTempoCap { get; init; }
    public required double HighAscensionBlockBonus { get; init; }

    public static AiCardRewardSynergyWeights CreateDefault()
    {
        return new AiCardRewardSynergyWeights
        {
            DrawWithHighCurveValue = 3.5d,
            EnergyWithHeavyCurveValue = 5d,
            AttackScalingSynergyPerAttack = 0.9d,
            DefenseScalingSynergyPerBlockSource = 0.9d,
            PowerWithDrawBonus = 3d,
            RetainWithHighCostBonus = 3d,
            ExhaustSynergyBonus = 2d,
            DamageNeedScale = 3d,
            DamageNeedCap = 12d,
            VulnerableNeedValue = 2d,
            BlockNeedScale = 3d,
            BlockNeedCap = 12d,
            WeakNeedValue = 2d,
            DexterityNeedValue = 4d,
            DrawNeedValue = 6d,
            EnergyNeedValue = 8d,
            ScalingNeedValue = 5d,
            PowerScalingBonus = 3d,
            EarlyCheapCardTempoScale = 0.35d,
            EarlyCheapCardTempoCap = 16d,
            EarlyActTempoScale = 0.15d,
            EarlyActTempoCap = 18d,
            HighAscensionBlockBonus = 1d
        };
    }

    public AiCardRewardSynergyWeights Merge(AiCardRewardSynergyWeightsOverrides? overrides)
    {
        return new AiCardRewardSynergyWeights
        {
            DrawWithHighCurveValue = AiCombatCoreWeights.ClampDouble(overrides?.DrawWithHighCurveValue, DrawWithHighCurveValue, -20d, 20d),
            EnergyWithHeavyCurveValue = AiCombatCoreWeights.ClampDouble(overrides?.EnergyWithHeavyCurveValue, EnergyWithHeavyCurveValue, -20d, 20d),
            AttackScalingSynergyPerAttack = AiCombatCoreWeights.ClampDouble(overrides?.AttackScalingSynergyPerAttack, AttackScalingSynergyPerAttack, -5d, 10d),
            DefenseScalingSynergyPerBlockSource = AiCombatCoreWeights.ClampDouble(overrides?.DefenseScalingSynergyPerBlockSource, DefenseScalingSynergyPerBlockSource, -5d, 10d),
            PowerWithDrawBonus = AiCombatCoreWeights.ClampDouble(overrides?.PowerWithDrawBonus, PowerWithDrawBonus, -20d, 20d),
            RetainWithHighCostBonus = AiCombatCoreWeights.ClampDouble(overrides?.RetainWithHighCostBonus, RetainWithHighCostBonus, -20d, 20d),
            ExhaustSynergyBonus = AiCombatCoreWeights.ClampDouble(overrides?.ExhaustSynergyBonus, ExhaustSynergyBonus, -20d, 20d),
            DamageNeedScale = AiCombatCoreWeights.ClampDouble(overrides?.DamageNeedScale, DamageNeedScale, 0.1d, 20d),
            DamageNeedCap = AiCombatCoreWeights.ClampDouble(overrides?.DamageNeedCap, DamageNeedCap, 0d, 50d),
            VulnerableNeedValue = AiCombatCoreWeights.ClampDouble(overrides?.VulnerableNeedValue, VulnerableNeedValue, -20d, 20d),
            BlockNeedScale = AiCombatCoreWeights.ClampDouble(overrides?.BlockNeedScale, BlockNeedScale, 0.1d, 20d),
            BlockNeedCap = AiCombatCoreWeights.ClampDouble(overrides?.BlockNeedCap, BlockNeedCap, 0d, 50d),
            WeakNeedValue = AiCombatCoreWeights.ClampDouble(overrides?.WeakNeedValue, WeakNeedValue, -20d, 20d),
            DexterityNeedValue = AiCombatCoreWeights.ClampDouble(overrides?.DexterityNeedValue, DexterityNeedValue, -20d, 20d),
            DrawNeedValue = AiCombatCoreWeights.ClampDouble(overrides?.DrawNeedValue, DrawNeedValue, -20d, 30d),
            EnergyNeedValue = AiCombatCoreWeights.ClampDouble(overrides?.EnergyNeedValue, EnergyNeedValue, -20d, 30d),
            ScalingNeedValue = AiCombatCoreWeights.ClampDouble(overrides?.ScalingNeedValue, ScalingNeedValue, -20d, 30d),
            PowerScalingBonus = AiCombatCoreWeights.ClampDouble(overrides?.PowerScalingBonus, PowerScalingBonus, -20d, 20d),
            EarlyCheapCardTempoScale = AiCombatCoreWeights.ClampDouble(overrides?.EarlyCheapCardTempoScale, EarlyCheapCardTempoScale, -5d, 5d),
            EarlyCheapCardTempoCap = AiCombatCoreWeights.ClampDouble(overrides?.EarlyCheapCardTempoCap, EarlyCheapCardTempoCap, 0d, 50d),
            EarlyActTempoScale = AiCombatCoreWeights.ClampDouble(overrides?.EarlyActTempoScale, EarlyActTempoScale, -5d, 5d),
            EarlyActTempoCap = AiCombatCoreWeights.ClampDouble(overrides?.EarlyActTempoCap, EarlyActTempoCap, 0d, 50d),
            HighAscensionBlockBonus = AiCombatCoreWeights.ClampDouble(overrides?.HighAscensionBlockBonus, HighAscensionBlockBonus, -10d, 10d)
        };
    }
}

internal sealed class AiCardRewardDisciplineWeights
{
    public required double DuplicatePenaltyPerCopy { get; init; }
    public required double ExcessDrawPenalty { get; init; }
    public required double ExcessEnergyPenalty { get; init; }
    public required double ExcessDamagePenaltyScale { get; init; }
    public required double ExcessDamagePenaltyCap { get; init; }
    public required double ExcessBlockPenaltyScale { get; init; }
    public required double ExcessBlockPenaltyCap { get; init; }
    public required double ExcessScalingPenalty { get; init; }
    public required double ExcessPowerPenalty { get; init; }
    public required double EtherealHighCostPenalty { get; init; }
    public required double RewardContextBonus { get; init; }
    public required double ChooseScreenContextBonus { get; init; }
    public required double EventContextBonus { get; init; }
    public required double ShopMissingCostPenalty { get; init; }
    public required double ShopCostRatioPenaltyScale { get; init; }
    public required double RewardSkipThreshold { get; init; }
    public required double ChooseScreenSkipThreshold { get; init; }
    public required double EventSkipThreshold { get; init; }
    public required double ShopSkipThresholdBase { get; init; }
    public required double ShopSkipThresholdCostFactor { get; init; }

    public static AiCardRewardDisciplineWeights CreateDefault()
    {
        return new AiCardRewardDisciplineWeights
        {
            DuplicatePenaltyPerCopy = 4d,
            ExcessDrawPenalty = 4d,
            ExcessEnergyPenalty = 5d,
            ExcessDamagePenaltyScale = 5d,
            ExcessDamagePenaltyCap = 8d,
            ExcessBlockPenaltyScale = 5d,
            ExcessBlockPenaltyCap = 8d,
            ExcessScalingPenalty = 6d,
            ExcessPowerPenalty = 4d,
            EtherealHighCostPenalty = 4d,
            RewardContextBonus = 2d,
            ChooseScreenContextBonus = 1d,
            EventContextBonus = 0d,
            ShopMissingCostPenalty = 6d,
            ShopCostRatioPenaltyScale = 8d,
            RewardSkipThreshold = 12d,
            ChooseScreenSkipThreshold = 12d,
            EventSkipThreshold = 14d,
            ShopSkipThresholdBase = 22d,
            ShopSkipThresholdCostFactor = 0.10d
        };
    }

    public AiCardRewardDisciplineWeights Merge(AiCardRewardDisciplineWeightsOverrides? overrides)
    {
        return new AiCardRewardDisciplineWeights
        {
            DuplicatePenaltyPerCopy = AiCombatCoreWeights.ClampDouble(overrides?.DuplicatePenaltyPerCopy, DuplicatePenaltyPerCopy, -10d, 20d),
            ExcessDrawPenalty = AiCombatCoreWeights.ClampDouble(overrides?.ExcessDrawPenalty, ExcessDrawPenalty, -20d, 20d),
            ExcessEnergyPenalty = AiCombatCoreWeights.ClampDouble(overrides?.ExcessEnergyPenalty, ExcessEnergyPenalty, -20d, 20d),
            ExcessDamagePenaltyScale = AiCombatCoreWeights.ClampDouble(overrides?.ExcessDamagePenaltyScale, ExcessDamagePenaltyScale, 0.1d, 20d),
            ExcessDamagePenaltyCap = AiCombatCoreWeights.ClampDouble(overrides?.ExcessDamagePenaltyCap, ExcessDamagePenaltyCap, 0d, 50d),
            ExcessBlockPenaltyScale = AiCombatCoreWeights.ClampDouble(overrides?.ExcessBlockPenaltyScale, ExcessBlockPenaltyScale, 0.1d, 20d),
            ExcessBlockPenaltyCap = AiCombatCoreWeights.ClampDouble(overrides?.ExcessBlockPenaltyCap, ExcessBlockPenaltyCap, 0d, 50d),
            ExcessScalingPenalty = AiCombatCoreWeights.ClampDouble(overrides?.ExcessScalingPenalty, ExcessScalingPenalty, -20d, 30d),
            ExcessPowerPenalty = AiCombatCoreWeights.ClampDouble(overrides?.ExcessPowerPenalty, ExcessPowerPenalty, -20d, 20d),
            EtherealHighCostPenalty = AiCombatCoreWeights.ClampDouble(overrides?.EtherealHighCostPenalty, EtherealHighCostPenalty, -20d, 20d),
            RewardContextBonus = AiCombatCoreWeights.ClampDouble(overrides?.RewardContextBonus, RewardContextBonus, -20d, 20d),
            ChooseScreenContextBonus = AiCombatCoreWeights.ClampDouble(overrides?.ChooseScreenContextBonus, ChooseScreenContextBonus, -20d, 20d),
            EventContextBonus = AiCombatCoreWeights.ClampDouble(overrides?.EventContextBonus, EventContextBonus, -20d, 20d),
            ShopMissingCostPenalty = AiCombatCoreWeights.ClampDouble(overrides?.ShopMissingCostPenalty, ShopMissingCostPenalty, 0d, 40d),
            ShopCostRatioPenaltyScale = AiCombatCoreWeights.ClampDouble(overrides?.ShopCostRatioPenaltyScale, ShopCostRatioPenaltyScale, 0d, 30d),
            RewardSkipThreshold = AiCombatCoreWeights.ClampDouble(overrides?.RewardSkipThreshold, RewardSkipThreshold, -50d, 80d),
            ChooseScreenSkipThreshold = AiCombatCoreWeights.ClampDouble(overrides?.ChooseScreenSkipThreshold, ChooseScreenSkipThreshold, -50d, 80d),
            EventSkipThreshold = AiCombatCoreWeights.ClampDouble(overrides?.EventSkipThreshold, EventSkipThreshold, -50d, 80d),
            ShopSkipThresholdBase = AiCombatCoreWeights.ClampDouble(overrides?.ShopSkipThresholdBase, ShopSkipThresholdBase, -50d, 120d),
            ShopSkipThresholdCostFactor = AiCombatCoreWeights.ClampDouble(overrides?.ShopSkipThresholdCostFactor, ShopSkipThresholdCostFactor, -2d, 5d)
        };
    }
}

internal sealed class AiPotionTuning
{
    public required AiPotionCombatUseWeights CombatUse { get; init; }
    public required AiPotionAcquisitionWeights Acquisition { get; init; }
    public required AiPotionRewardHandlingWeights RewardHandling { get; init; }

    public static AiPotionTuning CreateDefault()
    {
        return new AiPotionTuning
        {
            CombatUse = AiPotionCombatUseWeights.CreateDefault(),
            Acquisition = AiPotionAcquisitionWeights.CreateDefault(),
            RewardHandling = AiPotionRewardHandlingWeights.CreateDefault()
        };
    }

    public AiPotionTuning Merge(AiPotionTuningOverrides? overrides)
    {
        return new AiPotionTuning
        {
            CombatUse = CombatUse.Merge(overrides?.CombatUse),
            Acquisition = Acquisition.Merge(overrides?.Acquisition),
            RewardHandling = RewardHandling.Merge(overrides?.RewardHandling)
        };
    }
}

internal sealed class AiPotionCombatUseWeights
{
    public required int NormalFightBaseScore { get; init; }
    public required int EliteBossBaseScore { get; init; }
    public required int EliteBossBonus { get; init; }
    public required int GraveDangerDefensiveBonus { get; init; }
    public required int GraveDangerOffensiveBonus { get; init; }
    public required int EliteBossOffensiveFollowUpBonus { get; init; }
    public required int NormalFightOffensiveFollowUpBonus { get; init; }
    public required int AttackingTargetBonus { get; init; }
    public required int LowHealthTargetPenalty { get; init; }

    public static AiPotionCombatUseWeights CreateDefault()
    {
        return new AiPotionCombatUseWeights
        {
            NormalFightBaseScore = -160,
            EliteBossBaseScore = 18,
            EliteBossBonus = 12,
            GraveDangerDefensiveBonus = 160,
            GraveDangerOffensiveBonus = 60,
            EliteBossOffensiveFollowUpBonus = 95,
            NormalFightOffensiveFollowUpBonus = 25,
            AttackingTargetBonus = 8,
            LowHealthTargetPenalty = 18
        };
    }

    public AiPotionCombatUseWeights Merge(AiPotionCombatUseWeightsOverrides? overrides)
    {
        return new AiPotionCombatUseWeights
        {
            NormalFightBaseScore = AiCombatCoreWeights.ClampInt(overrides?.NormalFightBaseScore, NormalFightBaseScore, -500, 200),
            EliteBossBaseScore = AiCombatCoreWeights.ClampInt(overrides?.EliteBossBaseScore, EliteBossBaseScore, -100, 200),
            EliteBossBonus = AiCombatCoreWeights.ClampInt(overrides?.EliteBossBonus, EliteBossBonus, 0, 200),
            GraveDangerDefensiveBonus = AiCombatCoreWeights.ClampInt(overrides?.GraveDangerDefensiveBonus, GraveDangerDefensiveBonus, 0, 300),
            GraveDangerOffensiveBonus = AiCombatCoreWeights.ClampInt(overrides?.GraveDangerOffensiveBonus, GraveDangerOffensiveBonus, 0, 300),
            EliteBossOffensiveFollowUpBonus = AiCombatCoreWeights.ClampInt(overrides?.EliteBossOffensiveFollowUpBonus, EliteBossOffensiveFollowUpBonus, 0, 300),
            NormalFightOffensiveFollowUpBonus = AiCombatCoreWeights.ClampInt(overrides?.NormalFightOffensiveFollowUpBonus, NormalFightOffensiveFollowUpBonus, 0, 200),
            AttackingTargetBonus = AiCombatCoreWeights.ClampInt(overrides?.AttackingTargetBonus, AttackingTargetBonus, 0, 100),
            LowHealthTargetPenalty = AiCombatCoreWeights.ClampInt(overrides?.LowHealthTargetPenalty, LowHealthTargetPenalty, 0, 100)
        };
    }
}

internal sealed class AiPotionAcquisitionWeights
{
    public required double EventBaseline { get; init; }
    public required double RareBaseline { get; init; }
    public required double UncommonBaseline { get; init; }
    public required double CommonBaseline { get; init; }
    public required double FallbackBaseline { get; init; }
    public required double NoOpenSlotPenalty { get; init; }
    public required double SozuPenalty { get; init; }
    public required double DefensiveCoverageLowNeedBonus { get; init; }
    public required double DefensiveCoverageCoveredBonus { get; init; }
    public required double OffensiveCoverageLowNeedBonus { get; init; }
    public required double OffensiveCoverageCoveredBonus { get; init; }
    public required double TempoCoverageLowNeedBonus { get; init; }
    public required double TempoCoverageCoveredBonus { get; init; }
    public required double HighLeverageEmergencyBonus { get; init; }
    public required double ShopCostDivisor { get; init; }
    public required double UnaffordablePenalty { get; init; }
    public required double IllegalPenalty { get; init; }

    public static AiPotionAcquisitionWeights CreateDefault()
    {
        return new AiPotionAcquisitionWeights
        {
            EventBaseline = 14d,
            RareBaseline = 12d,
            UncommonBaseline = 8d,
            CommonBaseline = 5d,
            FallbackBaseline = 4d,
            NoOpenSlotPenalty = 20d,
            SozuPenalty = 30d,
            DefensiveCoverageLowNeedBonus = 6d,
            DefensiveCoverageCoveredBonus = 3d,
            OffensiveCoverageLowNeedBonus = 6d,
            OffensiveCoverageCoveredBonus = 3d,
            TempoCoverageLowNeedBonus = 7d,
            TempoCoverageCoveredBonus = 4d,
            HighLeverageEmergencyBonus = 8d,
            ShopCostDivisor = 11d,
            UnaffordablePenalty = 10d,
            IllegalPenalty = 18d
        };
    }

    public AiPotionAcquisitionWeights Merge(AiPotionAcquisitionWeightsOverrides? overrides)
    {
        return new AiPotionAcquisitionWeights
        {
            EventBaseline = AiCombatCoreWeights.ClampDouble(overrides?.EventBaseline, EventBaseline, -20d, 40d),
            RareBaseline = AiCombatCoreWeights.ClampDouble(overrides?.RareBaseline, RareBaseline, -20d, 40d),
            UncommonBaseline = AiCombatCoreWeights.ClampDouble(overrides?.UncommonBaseline, UncommonBaseline, -20d, 40d),
            CommonBaseline = AiCombatCoreWeights.ClampDouble(overrides?.CommonBaseline, CommonBaseline, -20d, 40d),
            FallbackBaseline = AiCombatCoreWeights.ClampDouble(overrides?.FallbackBaseline, FallbackBaseline, -20d, 40d),
            NoOpenSlotPenalty = AiCombatCoreWeights.ClampDouble(overrides?.NoOpenSlotPenalty, NoOpenSlotPenalty, 0d, 80d),
            SozuPenalty = AiCombatCoreWeights.ClampDouble(overrides?.SozuPenalty, SozuPenalty, 0d, 100d),
            DefensiveCoverageLowNeedBonus = AiCombatCoreWeights.ClampDouble(overrides?.DefensiveCoverageLowNeedBonus, DefensiveCoverageLowNeedBonus, -20d, 20d),
            DefensiveCoverageCoveredBonus = AiCombatCoreWeights.ClampDouble(overrides?.DefensiveCoverageCoveredBonus, DefensiveCoverageCoveredBonus, -20d, 20d),
            OffensiveCoverageLowNeedBonus = AiCombatCoreWeights.ClampDouble(overrides?.OffensiveCoverageLowNeedBonus, OffensiveCoverageLowNeedBonus, -20d, 20d),
            OffensiveCoverageCoveredBonus = AiCombatCoreWeights.ClampDouble(overrides?.OffensiveCoverageCoveredBonus, OffensiveCoverageCoveredBonus, -20d, 20d),
            TempoCoverageLowNeedBonus = AiCombatCoreWeights.ClampDouble(overrides?.TempoCoverageLowNeedBonus, TempoCoverageLowNeedBonus, -20d, 20d),
            TempoCoverageCoveredBonus = AiCombatCoreWeights.ClampDouble(overrides?.TempoCoverageCoveredBonus, TempoCoverageCoveredBonus, -20d, 20d),
            HighLeverageEmergencyBonus = AiCombatCoreWeights.ClampDouble(overrides?.HighLeverageEmergencyBonus, HighLeverageEmergencyBonus, -20d, 40d),
            ShopCostDivisor = AiCombatCoreWeights.ClampDouble(overrides?.ShopCostDivisor, ShopCostDivisor, 1d, 50d),
            UnaffordablePenalty = AiCombatCoreWeights.ClampDouble(overrides?.UnaffordablePenalty, UnaffordablePenalty, 0d, 60d),
            IllegalPenalty = AiCombatCoreWeights.ClampDouble(overrides?.IllegalPenalty, IllegalPenalty, 0d, 80d)
        };
    }
}

internal sealed class AiPotionRewardHandlingWeights
{
    public required double ReplacementThreshold { get; init; }

    public static AiPotionRewardHandlingWeights CreateDefault()
    {
        return new AiPotionRewardHandlingWeights
        {
            ReplacementThreshold = 1d
        };
    }

    public AiPotionRewardHandlingWeights Merge(AiPotionRewardHandlingWeightsOverrides? overrides)
    {
        return new AiPotionRewardHandlingWeights
        {
            ReplacementThreshold = AiCombatCoreWeights.ClampDouble(overrides?.ReplacementThreshold, ReplacementThreshold, -20d, 40d)
        };
    }
}

internal sealed class AiCardRewardTuningOverrides
{
    public AiCardRewardIntrinsicWeightsOverrides? IntrinsicWeights { get; set; }
    public AiCardRewardSynergyWeightsOverrides? SynergyWeights { get; set; }
    public AiCardRewardDisciplineWeightsOverrides? DisciplineWeights { get; set; }
}

internal sealed class AiCardRewardIntrinsicWeightsOverrides
{
    public double? DamageValuePerPoint { get; set; }
    public double? BlockValuePerPoint { get; set; }
    public double? DrawValue { get; set; }
    public double? EnergyValue { get; set; }
    public double? VulnerableValue { get; set; }
    public double? WeakValue { get; set; }
    public double? PersistentStrengthValue { get; set; }
    public double? PersistentDexterityValue { get; set; }
    public double? TemporaryStrengthValue { get; set; }
    public double? TemporaryDexterityValue { get; set; }
    public double? RepeatValue { get; set; }
    public double? PowerBonus { get; set; }
    public double? ZeroCostBonus { get; set; }
    public double? HighCostPenaltyPerExtraEnergy { get; set; }
    public double? RetainBonus { get; set; }
    public double? GoodExhaustBonus { get; set; }
    public double? BadExhaustPenalty { get; set; }
    public double? EtherealPenalty { get; set; }
    public double? UnknownValuePenalty { get; set; }
    public double? RareBonus { get; set; }
    public double? UncommonBonus { get; set; }
    public double? BasicBonus { get; set; }
    public double? CursePenalty { get; set; }
    public double? StatusPenalty { get; set; }
    public double? QuestPenalty { get; set; }
    public double? EventBonus { get; set; }
    public double? AncientBonus { get; set; }
}

internal sealed class AiCardRewardSynergyWeightsOverrides
{
    public double? DrawWithHighCurveValue { get; set; }
    public double? EnergyWithHeavyCurveValue { get; set; }
    public double? AttackScalingSynergyPerAttack { get; set; }
    public double? DefenseScalingSynergyPerBlockSource { get; set; }
    public double? PowerWithDrawBonus { get; set; }
    public double? RetainWithHighCostBonus { get; set; }
    public double? ExhaustSynergyBonus { get; set; }
    public double? DamageNeedScale { get; set; }
    public double? DamageNeedCap { get; set; }
    public double? VulnerableNeedValue { get; set; }
    public double? BlockNeedScale { get; set; }
    public double? BlockNeedCap { get; set; }
    public double? WeakNeedValue { get; set; }
    public double? DexterityNeedValue { get; set; }
    public double? DrawNeedValue { get; set; }
    public double? EnergyNeedValue { get; set; }
    public double? ScalingNeedValue { get; set; }
    public double? PowerScalingBonus { get; set; }
    public double? EarlyCheapCardTempoScale { get; set; }
    public double? EarlyCheapCardTempoCap { get; set; }
    public double? EarlyActTempoScale { get; set; }
    public double? EarlyActTempoCap { get; set; }
    public double? HighAscensionBlockBonus { get; set; }
}

internal sealed class AiCardRewardDisciplineWeightsOverrides
{
    public double? DuplicatePenaltyPerCopy { get; set; }
    public double? ExcessDrawPenalty { get; set; }
    public double? ExcessEnergyPenalty { get; set; }
    public double? ExcessDamagePenaltyScale { get; set; }
    public double? ExcessDamagePenaltyCap { get; set; }
    public double? ExcessBlockPenaltyScale { get; set; }
    public double? ExcessBlockPenaltyCap { get; set; }
    public double? ExcessScalingPenalty { get; set; }
    public double? ExcessPowerPenalty { get; set; }
    public double? EtherealHighCostPenalty { get; set; }
    public double? RewardContextBonus { get; set; }
    public double? ChooseScreenContextBonus { get; set; }
    public double? EventContextBonus { get; set; }
    public double? ShopMissingCostPenalty { get; set; }
    public double? ShopCostRatioPenaltyScale { get; set; }
    public double? RewardSkipThreshold { get; set; }
    public double? ChooseScreenSkipThreshold { get; set; }
    public double? EventSkipThreshold { get; set; }
    public double? ShopSkipThresholdBase { get; set; }
    public double? ShopSkipThresholdCostFactor { get; set; }
}

internal sealed class AiPotionTuningOverrides
{
    public AiPotionCombatUseWeightsOverrides? CombatUse { get; set; }
    public AiPotionAcquisitionWeightsOverrides? Acquisition { get; set; }
    public AiPotionRewardHandlingWeightsOverrides? RewardHandling { get; set; }
}

internal sealed class AiPotionCombatUseWeightsOverrides
{
    public int? NormalFightBaseScore { get; set; }
    public int? EliteBossBaseScore { get; set; }
    public int? EliteBossBonus { get; set; }
    public int? GraveDangerDefensiveBonus { get; set; }
    public int? GraveDangerOffensiveBonus { get; set; }
    public int? EliteBossOffensiveFollowUpBonus { get; set; }
    public int? NormalFightOffensiveFollowUpBonus { get; set; }
    public int? AttackingTargetBonus { get; set; }
    public int? LowHealthTargetPenalty { get; set; }
}

internal sealed class AiPotionAcquisitionWeightsOverrides
{
    public double? EventBaseline { get; set; }
    public double? RareBaseline { get; set; }
    public double? UncommonBaseline { get; set; }
    public double? CommonBaseline { get; set; }
    public double? FallbackBaseline { get; set; }
    public double? NoOpenSlotPenalty { get; set; }
    public double? SozuPenalty { get; set; }
    public double? DefensiveCoverageLowNeedBonus { get; set; }
    public double? DefensiveCoverageCoveredBonus { get; set; }
    public double? OffensiveCoverageLowNeedBonus { get; set; }
    public double? OffensiveCoverageCoveredBonus { get; set; }
    public double? TempoCoverageLowNeedBonus { get; set; }
    public double? TempoCoverageCoveredBonus { get; set; }
    public double? HighLeverageEmergencyBonus { get; set; }
    public double? ShopCostDivisor { get; set; }
    public double? UnaffordablePenalty { get; set; }
    public double? IllegalPenalty { get; set; }
}

internal sealed class AiPotionRewardHandlingWeightsOverrides
{
    public double? ReplacementThreshold { get; set; }
}
