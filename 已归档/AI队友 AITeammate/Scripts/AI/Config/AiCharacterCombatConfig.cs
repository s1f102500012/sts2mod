using System;
using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class AiCharacterCombatConfig
{
    public const int CurrentSchemaVersion = 4;
    public const string DefaultCharacterId = "default";
    public const string DefaultDisplayName = "Default";

    public required int SchemaVersion { get; init; }

    public required string CharacterId { get; init; }

    public required string DisplayName { get; init; }

    public required AiCharacterCombatTuning Combat { get; init; }

    public required AiCardRewardTuning CardRewards { get; init; }

    public required AiPotionTuning Potions { get; init; }

    public required AiShopTuning Shop { get; init; }

    public required AiEventTuning Events { get; init; }

    public static AiCharacterCombatConfig CreateBuiltInDefault(string characterId = DefaultCharacterId, string displayName = DefaultDisplayName)
    {
        return new AiCharacterCombatConfig
        {
            SchemaVersion = CurrentSchemaVersion,
            CharacterId = characterId,
            DisplayName = displayName,
            Combat = AiCharacterCombatTuning.CreateDefault(),
            CardRewards = AiCardRewardTuning.CreateDefault(),
            Potions = AiPotionTuning.CreateDefault(),
            Shop = AiShopTuning.CreateDefault(),
            Events = AiEventTuning.CreateDefault()
        };
    }
}

internal sealed class AiCharacterCombatTuning
{
    public required AiCombatCoreWeights CoreWeights { get; init; }

    public required AiCombatStatusWeights StatusWeights { get; init; }

    public required AiCombatResourceWeights ResourceWeights { get; init; }

    public required AiCombatRiskProfile RiskProfile { get; init; }

    public static AiCharacterCombatTuning CreateDefault()
    {
        return new AiCharacterCombatTuning
        {
            CoreWeights = AiCombatCoreWeights.CreateDefault(),
            StatusWeights = AiCombatStatusWeights.CreateDefault(),
            ResourceWeights = AiCombatResourceWeights.CreateDefault(),
            RiskProfile = AiCombatRiskProfile.CreateDefault()
        };
    }

    public AiCharacterCombatTuning Merge(AiCharacterCombatTuningOverrides? overrides)
    {
        return new AiCharacterCombatTuning
        {
            CoreWeights = CoreWeights.Merge(overrides?.CoreWeights),
            StatusWeights = StatusWeights.Merge(overrides?.StatusWeights),
            ResourceWeights = ResourceWeights.Merge(overrides?.ResourceWeights),
            RiskProfile = RiskProfile.Merge(overrides)
        };
    }
}

internal sealed class AiCombatCoreWeights
{
    public required int DirectDamageValuePerPoint { get; init; }

    public required int AttackWhileDefenseNeededPenalty { get; init; }

    public required int TargetLowHealthBiasThreshold { get; init; }

    public required int TargetLowHealthBiasValuePerPoint { get; init; }

    public required int AttackingTargetBonus { get; init; }

    public required int UtilityValueWhenThreatened { get; init; }

    public required int UtilityValueWhenSafe { get; init; }

    public required int LineDamageValuePerPoint { get; init; }

    public required int LeftoverBlockValuePerPoint { get; init; }

    public static AiCombatCoreWeights CreateDefault()
    {
        return new AiCombatCoreWeights
        {
            DirectDamageValuePerPoint = 5,
            AttackWhileDefenseNeededPenalty = 18,
            TargetLowHealthBiasThreshold = 24,
            TargetLowHealthBiasValuePerPoint = 1,
            AttackingTargetBonus = 8,
            UtilityValueWhenThreatened = 10,
            UtilityValueWhenSafe = 18,
            LineDamageValuePerPoint = 3,
            LeftoverBlockValuePerPoint = 4
        };
    }

    public AiCombatCoreWeights Merge(AiCombatCoreWeightsOverrides? overrides)
    {
        return new AiCombatCoreWeights
        {
            DirectDamageValuePerPoint = ClampInt(overrides?.DirectDamageValuePerPoint, DirectDamageValuePerPoint, 0, 50),
            AttackWhileDefenseNeededPenalty = ClampInt(overrides?.AttackWhileDefenseNeededPenalty, AttackWhileDefenseNeededPenalty, 0, 250),
            TargetLowHealthBiasThreshold = ClampInt(overrides?.TargetLowHealthBiasThreshold, TargetLowHealthBiasThreshold, 0, 500),
            TargetLowHealthBiasValuePerPoint = ClampInt(overrides?.TargetLowHealthBiasValuePerPoint, TargetLowHealthBiasValuePerPoint, 0, 20),
            AttackingTargetBonus = ClampInt(overrides?.AttackingTargetBonus, AttackingTargetBonus, 0, 100),
            UtilityValueWhenThreatened = ClampInt(overrides?.UtilityValueWhenThreatened, UtilityValueWhenThreatened, -100, 200),
            UtilityValueWhenSafe = ClampInt(overrides?.UtilityValueWhenSafe, UtilityValueWhenSafe, -100, 200),
            LineDamageValuePerPoint = ClampInt(overrides?.LineDamageValuePerPoint, LineDamageValuePerPoint, 0, 50),
            LeftoverBlockValuePerPoint = ClampInt(overrides?.LeftoverBlockValuePerPoint, LeftoverBlockValuePerPoint, 0, 50)
        };
    }

    internal static int ClampInt(int? candidate, int fallback, int min, int max)
    {
        if (!candidate.HasValue)
        {
            return fallback;
        }

        return Math.Clamp(candidate.Value, min, max);
    }

    internal static double ClampDouble(double? candidate, double fallback, double min, double max)
    {
        if (!candidate.HasValue || double.IsNaN(candidate.Value) || double.IsInfinity(candidate.Value))
        {
            return fallback;
        }

        return Math.Clamp(candidate.Value, min, max);
    }
}

internal sealed class AiCombatStatusWeights
{
    public required int StrengthPerHitValue { get; init; }

    public required int SelfTemporaryStrengthValue { get; init; }

    public required int VulnerableWithFollowUpValue { get; init; }

    public required int VulnerableWithoutFollowUpValue { get; init; }

    public required int WeakImmediateDefenseValue { get; init; }

    public required int WeakDebuffValue { get; init; }

    public required int TemporaryStrengthPerAffordableAttackValue { get; init; }

    public required int TemporaryStrengthMinimumValue { get; init; }

    public required int PersistentStrengthPerAffordableAttackValue { get; init; }

    public required int PersistentStrengthMinimumValue { get; init; }

    public required int TemporaryDexterityPerAffordableBlockValue { get; init; }

    public required int TemporaryDexterityMinimumValue { get; init; }

    public required int PersistentDexterityPerAffordableBlockValue { get; init; }

    public required int PersistentDexterityMinimumValue { get; init; }

    public required int TemporaryDexterityWithFollowUpBlockValue { get; init; }

    public required int TemporaryDexterityThreatenedBlockValue { get; init; }

    public required int TemporaryDexteritySafeBlockValue { get; init; }

    public required int PersistentDexterityWithBlockValue { get; init; }

    public required int PersistentDexterityWithoutBlockValue { get; init; }

    public required int SetupPersistentStrengthValue { get; init; }

    public required int SetupTemporaryStrengthValue { get; init; }

    public required int SetupPersistentDexterityValue { get; init; }

    public required int SetupTemporaryDexterityValue { get; init; }

    public required int LinePersistentStrengthValue { get; init; }

    public required int LineTemporaryStrengthValue { get; init; }

    public required int LinePersistentDexterityValue { get; init; }

    public required int LineTemporaryDexterityThreatenedValue { get; init; }

    public required int LineTemporaryDexteritySafeValue { get; init; }

    public static AiCombatStatusWeights CreateDefault()
    {
        return new AiCombatStatusWeights
        {
            StrengthPerHitValue = 1,
            SelfTemporaryStrengthValue = 8,
            VulnerableWithFollowUpValue = 16,
            VulnerableWithoutFollowUpValue = 6,
            WeakImmediateDefenseValue = 12,
            WeakDebuffValue = 5,
            TemporaryStrengthPerAffordableAttackValue = 8,
            TemporaryStrengthMinimumValue = 6,
            PersistentStrengthPerAffordableAttackValue = 5,
            PersistentStrengthMinimumValue = 4,
            TemporaryDexterityPerAffordableBlockValue = 10,
            TemporaryDexterityMinimumValue = 8,
            PersistentDexterityPerAffordableBlockValue = 6,
            PersistentDexterityMinimumValue = 4,
            TemporaryDexterityWithFollowUpBlockValue = 18,
            TemporaryDexterityThreatenedBlockValue = 12,
            TemporaryDexteritySafeBlockValue = 6,
            PersistentDexterityWithBlockValue = 10,
            PersistentDexterityWithoutBlockValue = 4,
            SetupPersistentStrengthValue = 5,
            SetupTemporaryStrengthValue = 8,
            SetupPersistentDexterityValue = 4,
            SetupTemporaryDexterityValue = 8,
            LinePersistentStrengthValue = 10,
            LineTemporaryStrengthValue = 16,
            LinePersistentDexterityValue = 8,
            LineTemporaryDexterityThreatenedValue = 18,
            LineTemporaryDexteritySafeValue = 10
        };
    }

    public AiCombatStatusWeights Merge(AiCombatStatusWeightsOverrides? overrides)
    {
        return new AiCombatStatusWeights
        {
            StrengthPerHitValue = AiCombatCoreWeights.ClampInt(overrides?.StrengthPerHitValue, StrengthPerHitValue, 0, 20),
            SelfTemporaryStrengthValue = AiCombatCoreWeights.ClampInt(overrides?.SelfTemporaryStrengthValue, SelfTemporaryStrengthValue, 0, 100),
            VulnerableWithFollowUpValue = AiCombatCoreWeights.ClampInt(overrides?.VulnerableWithFollowUpValue, VulnerableWithFollowUpValue, 0, 100),
            VulnerableWithoutFollowUpValue = AiCombatCoreWeights.ClampInt(overrides?.VulnerableWithoutFollowUpValue, VulnerableWithoutFollowUpValue, 0, 100),
            WeakImmediateDefenseValue = AiCombatCoreWeights.ClampInt(overrides?.WeakImmediateDefenseValue, WeakImmediateDefenseValue, 0, 100),
            WeakDebuffValue = AiCombatCoreWeights.ClampInt(overrides?.WeakDebuffValue, WeakDebuffValue, 0, 100),
            TemporaryStrengthPerAffordableAttackValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryStrengthPerAffordableAttackValue, TemporaryStrengthPerAffordableAttackValue, 0, 100),
            TemporaryStrengthMinimumValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryStrengthMinimumValue, TemporaryStrengthMinimumValue, 0, 100),
            PersistentStrengthPerAffordableAttackValue = AiCombatCoreWeights.ClampInt(overrides?.PersistentStrengthPerAffordableAttackValue, PersistentStrengthPerAffordableAttackValue, 0, 100),
            PersistentStrengthMinimumValue = AiCombatCoreWeights.ClampInt(overrides?.PersistentStrengthMinimumValue, PersistentStrengthMinimumValue, 0, 100),
            TemporaryDexterityPerAffordableBlockValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryDexterityPerAffordableBlockValue, TemporaryDexterityPerAffordableBlockValue, 0, 100),
            TemporaryDexterityMinimumValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryDexterityMinimumValue, TemporaryDexterityMinimumValue, 0, 100),
            PersistentDexterityPerAffordableBlockValue = AiCombatCoreWeights.ClampInt(overrides?.PersistentDexterityPerAffordableBlockValue, PersistentDexterityPerAffordableBlockValue, 0, 100),
            PersistentDexterityMinimumValue = AiCombatCoreWeights.ClampInt(overrides?.PersistentDexterityMinimumValue, PersistentDexterityMinimumValue, 0, 100),
            TemporaryDexterityWithFollowUpBlockValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryDexterityWithFollowUpBlockValue, TemporaryDexterityWithFollowUpBlockValue, 0, 100),
            TemporaryDexterityThreatenedBlockValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryDexterityThreatenedBlockValue, TemporaryDexterityThreatenedBlockValue, 0, 100),
            TemporaryDexteritySafeBlockValue = AiCombatCoreWeights.ClampInt(overrides?.TemporaryDexteritySafeBlockValue, TemporaryDexteritySafeBlockValue, 0, 100),
            PersistentDexterityWithBlockValue = AiCombatCoreWeights.ClampInt(overrides?.PersistentDexterityWithBlockValue, PersistentDexterityWithBlockValue, 0, 100),
            PersistentDexterityWithoutBlockValue = AiCombatCoreWeights.ClampInt(overrides?.PersistentDexterityWithoutBlockValue, PersistentDexterityWithoutBlockValue, 0, 100),
            SetupPersistentStrengthValue = AiCombatCoreWeights.ClampInt(overrides?.SetupPersistentStrengthValue, SetupPersistentStrengthValue, 0, 100),
            SetupTemporaryStrengthValue = AiCombatCoreWeights.ClampInt(overrides?.SetupTemporaryStrengthValue, SetupTemporaryStrengthValue, 0, 100),
            SetupPersistentDexterityValue = AiCombatCoreWeights.ClampInt(overrides?.SetupPersistentDexterityValue, SetupPersistentDexterityValue, 0, 100),
            SetupTemporaryDexterityValue = AiCombatCoreWeights.ClampInt(overrides?.SetupTemporaryDexterityValue, SetupTemporaryDexterityValue, 0, 100),
            LinePersistentStrengthValue = AiCombatCoreWeights.ClampInt(overrides?.LinePersistentStrengthValue, LinePersistentStrengthValue, 0, 100),
            LineTemporaryStrengthValue = AiCombatCoreWeights.ClampInt(overrides?.LineTemporaryStrengthValue, LineTemporaryStrengthValue, 0, 150),
            LinePersistentDexterityValue = AiCombatCoreWeights.ClampInt(overrides?.LinePersistentDexterityValue, LinePersistentDexterityValue, 0, 100),
            LineTemporaryDexterityThreatenedValue = AiCombatCoreWeights.ClampInt(overrides?.LineTemporaryDexterityThreatenedValue, LineTemporaryDexterityThreatenedValue, 0, 150),
            LineTemporaryDexteritySafeValue = AiCombatCoreWeights.ClampInt(overrides?.LineTemporaryDexteritySafeValue, LineTemporaryDexteritySafeValue, 0, 150)
        };
    }
}

internal sealed class AiCombatResourceWeights
{
    public required int DrawValueWhenPlayable { get; init; }

    public required int DrawPenaltyWhenNotPlayable { get; init; }

    public required int EnergyGainValue { get; init; }

    public required int EnergyEfficiencyValue { get; init; }

    public required int SetupActionBonus { get; init; }

    public required int SetupDrawValueWhenPlayable { get; init; }

    public required int SetupDrawPenaltyWhenNotPlayable { get; init; }

    public required int SetupEnergyGainValue { get; init; }

    public required int LineEnergyGeneratedValue { get; init; }

    public required int LineCardsDrawnValueWhenUsable { get; init; }

    public required int LineCardsDrawnPenaltyWhenNotUsable { get; init; }

    public required int RemainingEnergyPenalty { get; init; }

    public required int RemainingAffordableActionsPenalty { get; init; }

    public required int EndTurnWhenSkippingPotionsBonus { get; init; }

    public required int EndTurnWhileOtherActionsExistPenalty { get; init; }

    public static AiCombatResourceWeights CreateDefault()
    {
        return new AiCombatResourceWeights
        {
            DrawValueWhenPlayable = 10,
            DrawPenaltyWhenNotPlayable = 8,
            EnergyGainValue = 18,
            EnergyEfficiencyValue = 4,
            SetupActionBonus = 10,
            SetupDrawValueWhenPlayable = 10,
            SetupDrawPenaltyWhenNotPlayable = 10,
            SetupEnergyGainValue = 14,
            LineEnergyGeneratedValue = 14,
            LineCardsDrawnValueWhenUsable = 4,
            LineCardsDrawnPenaltyWhenNotUsable = 4,
            RemainingEnergyPenalty = 18,
            RemainingAffordableActionsPenalty = 24,
            EndTurnWhenSkippingPotionsBonus = 24,
            EndTurnWhileOtherActionsExistPenalty = 10000
        };
    }

    public AiCombatResourceWeights Merge(AiCombatResourceWeightsOverrides? overrides)
    {
        return new AiCombatResourceWeights
        {
            DrawValueWhenPlayable = AiCombatCoreWeights.ClampInt(overrides?.DrawValueWhenPlayable, DrawValueWhenPlayable, 0, 100),
            DrawPenaltyWhenNotPlayable = AiCombatCoreWeights.ClampInt(overrides?.DrawPenaltyWhenNotPlayable, DrawPenaltyWhenNotPlayable, 0, 100),
            EnergyGainValue = AiCombatCoreWeights.ClampInt(overrides?.EnergyGainValue, EnergyGainValue, 0, 100),
            EnergyEfficiencyValue = AiCombatCoreWeights.ClampInt(overrides?.EnergyEfficiencyValue, EnergyEfficiencyValue, 0, 50),
            SetupActionBonus = AiCombatCoreWeights.ClampInt(overrides?.SetupActionBonus, SetupActionBonus, 0, 100),
            SetupDrawValueWhenPlayable = AiCombatCoreWeights.ClampInt(overrides?.SetupDrawValueWhenPlayable, SetupDrawValueWhenPlayable, 0, 100),
            SetupDrawPenaltyWhenNotPlayable = AiCombatCoreWeights.ClampInt(overrides?.SetupDrawPenaltyWhenNotPlayable, SetupDrawPenaltyWhenNotPlayable, 0, 100),
            SetupEnergyGainValue = AiCombatCoreWeights.ClampInt(overrides?.SetupEnergyGainValue, SetupEnergyGainValue, 0, 100),
            LineEnergyGeneratedValue = AiCombatCoreWeights.ClampInt(overrides?.LineEnergyGeneratedValue, LineEnergyGeneratedValue, 0, 100),
            LineCardsDrawnValueWhenUsable = AiCombatCoreWeights.ClampInt(overrides?.LineCardsDrawnValueWhenUsable, LineCardsDrawnValueWhenUsable, 0, 100),
            LineCardsDrawnPenaltyWhenNotUsable = AiCombatCoreWeights.ClampInt(overrides?.LineCardsDrawnPenaltyWhenNotUsable, LineCardsDrawnPenaltyWhenNotUsable, 0, 100),
            RemainingEnergyPenalty = AiCombatCoreWeights.ClampInt(overrides?.RemainingEnergyPenalty, RemainingEnergyPenalty, 0, 200),
            RemainingAffordableActionsPenalty = AiCombatCoreWeights.ClampInt(overrides?.RemainingAffordableActionsPenalty, RemainingAffordableActionsPenalty, 0, 200),
            EndTurnWhenSkippingPotionsBonus = AiCombatCoreWeights.ClampInt(overrides?.EndTurnWhenSkippingPotionsBonus, EndTurnWhenSkippingPotionsBonus, -100, 200),
            EndTurnWhileOtherActionsExistPenalty = AiCombatCoreWeights.ClampInt(overrides?.EndTurnWhileOtherActionsExistPenalty, EndTurnWhileOtherActionsExistPenalty, 0, 100000)
        };
    }
}

internal sealed class AiCombatRiskProfile
{
    public required double SurvivalWeight { get; init; }

    public required double DefenseWeight { get; init; }

    public required double AttackWeight { get; init; }

    public required double Aggressiveness { get; init; }

    public required int LethalPriorityBonus { get; init; }

    public required int LethalIncomingDamageValue { get; init; }

    public required int BlockedDamageValuePerPoint { get; init; }

    public required int ExcessBlockValuePerPoint { get; init; }

    public required int FullBlockCoverageBonus { get; init; }

    public required int LowHealthEmergencyDefenseBonus { get; init; }

    public required int PreventedDamageValuePerPoint { get; init; }

    public required int DamageTakenPenaltyPerPoint { get; init; }

    public required int KillPreventionValuePerPoint { get; init; }

    public required int WeakPreventionValuePerPoint { get; init; }

    public required int DeadEnemyReward { get; init; }

    public required int PerfectDefenseBonus { get; init; }

    public required int ExposedDamageWithoutDefensePenalty { get; init; }

    public static AiCombatRiskProfile CreateDefault()
    {
        return new AiCombatRiskProfile
        {
            SurvivalWeight = 1.0d,
            DefenseWeight = 1.0d,
            AttackWeight = 1.0d,
            Aggressiveness = 1.0d,
            LethalPriorityBonus = 55,
            LethalIncomingDamageValue = 8,
            BlockedDamageValuePerPoint = 10,
            ExcessBlockValuePerPoint = 2,
            FullBlockCoverageBonus = 50,
            LowHealthEmergencyDefenseBonus = 35,
            PreventedDamageValuePerPoint = 18,
            DamageTakenPenaltyPerPoint = 30,
            KillPreventionValuePerPoint = 10,
            WeakPreventionValuePerPoint = 10,
            DeadEnemyReward = 45,
            PerfectDefenseBonus = 60,
            ExposedDamageWithoutDefensePenalty = 35
        };
    }

    public AiCombatRiskProfile Merge(AiCharacterCombatTuningOverrides? overrides)
    {
        AiCombatRiskProfileOverrides? nested = overrides?.RiskProfile;
        return new AiCombatRiskProfile
        {
            SurvivalWeight = AiCombatCoreWeights.ClampDouble(nested?.SurvivalWeight ?? overrides?.SurvivalWeight, SurvivalWeight, 0.1d, 5.0d),
            DefenseWeight = AiCombatCoreWeights.ClampDouble(nested?.DefenseWeight ?? overrides?.DefenseWeight, DefenseWeight, 0.1d, 5.0d),
            AttackWeight = AiCombatCoreWeights.ClampDouble(nested?.AttackWeight ?? overrides?.AttackWeight, AttackWeight, 0.1d, 5.0d),
            Aggressiveness = AiCombatCoreWeights.ClampDouble(nested?.Aggressiveness ?? overrides?.Aggressiveness, Aggressiveness, 0.1d, 5.0d),
            LethalPriorityBonus = AiCombatCoreWeights.ClampInt(nested?.LethalPriorityBonus ?? overrides?.LethalPriorityBonus, LethalPriorityBonus, 0, 500),
            LethalIncomingDamageValue = AiCombatCoreWeights.ClampInt(nested?.LethalIncomingDamageValue, LethalIncomingDamageValue, 0, 100),
            BlockedDamageValuePerPoint = AiCombatCoreWeights.ClampInt(nested?.BlockedDamageValuePerPoint, BlockedDamageValuePerPoint, 0, 100),
            ExcessBlockValuePerPoint = AiCombatCoreWeights.ClampInt(nested?.ExcessBlockValuePerPoint, ExcessBlockValuePerPoint, 0, 50),
            FullBlockCoverageBonus = AiCombatCoreWeights.ClampInt(nested?.FullBlockCoverageBonus, FullBlockCoverageBonus, 0, 250),
            LowHealthEmergencyDefenseBonus = AiCombatCoreWeights.ClampInt(nested?.LowHealthEmergencyDefenseBonus, LowHealthEmergencyDefenseBonus, 0, 250),
            PreventedDamageValuePerPoint = AiCombatCoreWeights.ClampInt(nested?.PreventedDamageValuePerPoint, PreventedDamageValuePerPoint, 0, 100),
            DamageTakenPenaltyPerPoint = AiCombatCoreWeights.ClampInt(nested?.DamageTakenPenaltyPerPoint, DamageTakenPenaltyPerPoint, 0, 200),
            KillPreventionValuePerPoint = AiCombatCoreWeights.ClampInt(nested?.KillPreventionValuePerPoint, KillPreventionValuePerPoint, 0, 100),
            WeakPreventionValuePerPoint = AiCombatCoreWeights.ClampInt(nested?.WeakPreventionValuePerPoint, WeakPreventionValuePerPoint, 0, 100),
            DeadEnemyReward = AiCombatCoreWeights.ClampInt(nested?.DeadEnemyReward, DeadEnemyReward, 0, 250),
            PerfectDefenseBonus = AiCombatCoreWeights.ClampInt(nested?.PerfectDefenseBonus, PerfectDefenseBonus, 0, 250),
            ExposedDamageWithoutDefensePenalty = AiCombatCoreWeights.ClampInt(nested?.ExposedDamageWithoutDefensePenalty, ExposedDamageWithoutDefensePenalty, 0, 250)
        };
    }

    public int ApplyAttackWeight(int score)
    {
        return Scale(score, AttackWeight * Aggressiveness);
    }

    public int ApplyDefenseWeight(int score)
    {
        return Scale(score, DefenseWeight);
    }

    public int ApplySurvivalWeight(int score)
    {
        return Scale(score, SurvivalWeight);
    }

    private static int Scale(int value, double multiplier)
    {
        return (int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero);
    }
}

internal sealed class AiCharacterCombatConfigFile
{
    public int? SchemaVersion { get; set; }

    public string? CharacterId { get; set; }

    public string? DisplayName { get; set; }

    public AiCharacterCombatTuningOverrides? Combat { get; set; }

    public AiCardRewardTuningOverrides? CardRewards { get; set; }

    public AiPotionTuningOverrides? Potions { get; set; }

    public AiShopTuningOverrides? Shop { get; set; }

    public AiEventTuningOverrides? Events { get; set; }
}

internal sealed class AiCharacterCombatTuningOverrides
{
    public AiCombatCoreWeightsOverrides? CoreWeights { get; set; }

    public AiCombatStatusWeightsOverrides? StatusWeights { get; set; }

    public AiCombatResourceWeightsOverrides? ResourceWeights { get; set; }

    public AiCombatRiskProfileOverrides? RiskProfile { get; set; }

    public double? SurvivalWeight { get; set; }

    public double? DefenseWeight { get; set; }

    public double? AttackWeight { get; set; }

    public double? Aggressiveness { get; set; }

    public int? LethalPriorityBonus { get; set; }
}

internal sealed class AiCombatCoreWeightsOverrides
{
    public int? DirectDamageValuePerPoint { get; set; }

    public int? AttackWhileDefenseNeededPenalty { get; set; }

    public int? TargetLowHealthBiasThreshold { get; set; }

    public int? TargetLowHealthBiasValuePerPoint { get; set; }

    public int? AttackingTargetBonus { get; set; }

    public int? UtilityValueWhenThreatened { get; set; }

    public int? UtilityValueWhenSafe { get; set; }

    public int? LineDamageValuePerPoint { get; set; }

    public int? LeftoverBlockValuePerPoint { get; set; }
}

internal sealed class AiCombatStatusWeightsOverrides
{
    public int? StrengthPerHitValue { get; set; }

    public int? SelfTemporaryStrengthValue { get; set; }

    public int? VulnerableWithFollowUpValue { get; set; }

    public int? VulnerableWithoutFollowUpValue { get; set; }

    public int? WeakImmediateDefenseValue { get; set; }

    public int? WeakDebuffValue { get; set; }

    public int? TemporaryStrengthPerAffordableAttackValue { get; set; }

    public int? TemporaryStrengthMinimumValue { get; set; }

    public int? PersistentStrengthPerAffordableAttackValue { get; set; }

    public int? PersistentStrengthMinimumValue { get; set; }

    public int? TemporaryDexterityPerAffordableBlockValue { get; set; }

    public int? TemporaryDexterityMinimumValue { get; set; }

    public int? PersistentDexterityPerAffordableBlockValue { get; set; }

    public int? PersistentDexterityMinimumValue { get; set; }

    public int? TemporaryDexterityWithFollowUpBlockValue { get; set; }

    public int? TemporaryDexterityThreatenedBlockValue { get; set; }

    public int? TemporaryDexteritySafeBlockValue { get; set; }

    public int? PersistentDexterityWithBlockValue { get; set; }

    public int? PersistentDexterityWithoutBlockValue { get; set; }

    public int? SetupPersistentStrengthValue { get; set; }

    public int? SetupTemporaryStrengthValue { get; set; }

    public int? SetupPersistentDexterityValue { get; set; }

    public int? SetupTemporaryDexterityValue { get; set; }

    public int? LinePersistentStrengthValue { get; set; }

    public int? LineTemporaryStrengthValue { get; set; }

    public int? LinePersistentDexterityValue { get; set; }

    public int? LineTemporaryDexterityThreatenedValue { get; set; }

    public int? LineTemporaryDexteritySafeValue { get; set; }
}

internal sealed class AiCombatResourceWeightsOverrides
{
    public int? DrawValueWhenPlayable { get; set; }

    public int? DrawPenaltyWhenNotPlayable { get; set; }

    public int? EnergyGainValue { get; set; }

    public int? EnergyEfficiencyValue { get; set; }

    public int? SetupActionBonus { get; set; }

    public int? SetupDrawValueWhenPlayable { get; set; }

    public int? SetupDrawPenaltyWhenNotPlayable { get; set; }

    public int? SetupEnergyGainValue { get; set; }

    public int? LineEnergyGeneratedValue { get; set; }

    public int? LineCardsDrawnValueWhenUsable { get; set; }

    public int? LineCardsDrawnPenaltyWhenNotUsable { get; set; }

    public int? RemainingEnergyPenalty { get; set; }

    public int? RemainingAffordableActionsPenalty { get; set; }

    public int? EndTurnWhenSkippingPotionsBonus { get; set; }

    public int? EndTurnWhileOtherActionsExistPenalty { get; set; }
}

internal sealed class AiCombatRiskProfileOverrides
{
    public double? SurvivalWeight { get; set; }

    public double? DefenseWeight { get; set; }

    public double? AttackWeight { get; set; }

    public double? Aggressiveness { get; set; }

    public int? LethalPriorityBonus { get; set; }

    public int? LethalIncomingDamageValue { get; set; }

    public int? BlockedDamageValuePerPoint { get; set; }

    public int? ExcessBlockValuePerPoint { get; set; }

    public int? FullBlockCoverageBonus { get; set; }

    public int? LowHealthEmergencyDefenseBonus { get; set; }

    public int? PreventedDamageValuePerPoint { get; set; }

    public int? DamageTakenPenaltyPerPoint { get; set; }

    public int? KillPreventionValuePerPoint { get; set; }

    public int? WeakPreventionValuePerPoint { get; set; }

    public int? DeadEnemyReward { get; set; }

    public int? PerfectDefenseBonus { get; set; }

    public int? ExposedDamageWithoutDefensePenalty { get; set; }
}

internal static class AiCharacterCombatConfigCatalog
{
    public static IEnumerable<AiCharacterCombatConfig> BuiltInFiles
    {
        get
        {
            yield return AiCharacterCombatConfig.CreateBuiltInDefault();

            foreach (AiTeammatePlaceholderCharacter character in AiTeammatePlaceholderCharacters.All)
            {
                yield return AiCharacterCombatConfig.CreateBuiltInDefault(character.Id, character.DisplayName);
            }
        }
    }
}
