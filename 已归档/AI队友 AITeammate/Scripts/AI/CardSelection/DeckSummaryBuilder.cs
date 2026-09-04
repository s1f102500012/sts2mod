using System;
using System.Collections.Generic;
using System.Linq;

namespace AITeammate.Scripts;

internal static class DeckSummaryBuilder
{
    public static DeckSummary Build(IReadOnlyList<ResolvedCardView> cards)
    {
        if (cards.Count == 0)
        {
            return new DeckSummary();
        }

        int totalCost = 0;
        int costedCards = 0;
        int totalDamage = 0;
        int totalBlock = 0;

        foreach (ResolvedCardView card in cards)
        {
            if (card.EffectiveCost >= 0)
            {
                totalCost += card.EffectiveCost;
                costedCards++;
            }

            totalDamage += card.GetEstimatedDamage();
            totalBlock += card.GetEstimatedProtection();
        }

        return new DeckSummary
        {
            CardCount = cards.Count,
            UpgradedCardCount = cards.Count(static card => card.IsUpgraded),
            AttackCount = cards.Count(static card => card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack),
            SkillCount = cards.Count(static card => card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Skill),
            PowerCount = cards.Count(static card => card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Power),
            FrontloadDamageSources = cards.Count(static card => card.GetEstimatedDamage() > 0),
            QualityDamageSources = cards.Count(IsQualityDamageSource),
            BlockSources = cards.Count(static card => card.GetEstimatedProtection() > 0),
            QualityDefenseSources = cards.Count(IsQualityDefenseSource),
            DrawSources = cards.Count(static card => card.GetCardsDrawn() > 0),
            EnergySources = cards.Count(static card => card.GetEnergyGain() > 0),
            VulnerableSources = cards.Count(static card => card.GetEnemyVulnerableAmount() > 0),
            WeakSources = cards.Count(static card => card.GetEnemyWeakAmount() > 0),
            ScalingSources = cards.Count(IsScalingCard),
            AoESources = cards.Count(static card => card.DealsDamageToAllEnemies()),
            BadCards = cards.Count(static card => card.Rarity is "Curse" or "Status" || card.Ethereal && card.GetEstimatedDamage() + card.GetEstimatedProtection() <= 6),
            ControlledHandCleanupCards = cards.Count(StatusCardStrategy.IsControlledHandCleanupCard),
            StatusHandlingCards = cards.Count(StatusCardStrategy.IsLikelyHandCleanupCard),
            BasicCards = cards.Count(IsBasicStarterCard),
            StrikeCards = cards.Count(static card => IsCardIdLike(card, "STRIKE")),
            DefendCards = cards.Count(static card => IsCardIdLike(card, "DEFEND")),
            ExhaustPayoffCards = cards.Count(IsExhaustPayoffCard),
            RetainCards = cards.Count(static card => card.Retain),
            ExhaustCards = cards.Count(static card => card.Exhaust),
            ZeroCostCards = cards.Count(static card => card.EffectiveCost == 0),
            HighCostCards = cards.Count(static card => card.EffectiveCost >= 2),
            EngineCards = cards.Count(IsEngineCard),
            OrbCards = cards.Count(IsOrbCard),
            FocusCards = cards.Count(IsFocusCard),
            OrbSlotCards = cards.Count(IsOrbSlotCard),
            PowerPayoffCards = cards.Count(IsPowerPayoffCard),
            RecursionCards = cards.Count(IsRecursionCard),
            AverageCost = costedCards > 0 ? (double)totalCost / costedCards : 0d,
            AverageDamage = (double)totalDamage / cards.Count,
            AverageBlock = (double)totalBlock / cards.Count
        };
    }

    private static bool IsScalingCard(ResolvedCardView card)
    {
        int persistentStrength = Math.Max(0, card.GetSelfStrengthAmount() - card.GetSelfTemporaryStrengthAmount());
        int persistentDexterity = Math.Max(0, card.GetSelfDexterityAmount() - card.GetSelfTemporaryDexterityAmount());
        return card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Power ||
               persistentStrength > 0 ||
               persistentDexterity > 0 ||
               RegentCharacterStrategy.IsScalingCard(card) ||
               (card.GetEnergyGain() > 0 && card.Effects.Any(static effect => effect.ValueTiming != ValueTiming.Immediate));
    }

    private static bool IsQualityDamageSource(ResolvedCardView card)
    {
        int damage = card.GetEstimatedDamage();
        if (damage <= 0)
        {
            return false;
        }

        if (card.DealsDamageToAllEnemies() || card.GetEnemyVulnerableAmount() > 0)
        {
            return true;
        }

        if (IsBasicStarterCard(card))
        {
            return false;
        }

        return damage >= 10 || card.EffectiveCost <= 1 && damage >= 8 || RegentCharacterStrategy.IsScalingCard(card);
    }

    private static bool IsQualityDefenseSource(ResolvedCardView card)
    {
        int block = card.GetEstimatedProtection();
        if (block >= 8 ||
            card.GetEnemyWeakAmount() > 0 ||
            Math.Max(0, card.GetSelfDexterityAmount() - card.GetSelfTemporaryDexterityAmount()) > 0)
        {
            return true;
        }

        if (block >= 5 && card.GetCardsDrawn() > 0)
        {
            return true;
        }

        return IsFrostCard(card);
    }

    private static bool IsBasicStarterCard(ResolvedCardView card)
    {
        return card.Rarity == "Basic" ||
               IsCardIdLike(card, "STRIKE") ||
               IsCardIdLike(card, "DEFEND");
    }

    private static bool IsExhaustPayoffCard(ResolvedCardView card)
    {
        return IsCardIdLike(card, "FEEL_NO_PAIN") ||
               IsCardIdLike(card, "DARK_EMBRACE") ||
               IsCardIdLike(card, "CORRUPTION") ||
               IsCardIdLike(card, "CHARON") ||
               IsCardIdLike(card, "BURNING_PACT") ||
               IsCardIdLike(card, "SECOND_WIND") ||
               IsCardIdLike(card, "FIEND_FIRE");
    }

    private static bool IsEngineCard(ResolvedCardView card)
    {
        return card.GetCardsDrawn() > 0 ||
               card.GetEnergyGain() > 0 ||
               card.GetStarsGenerated() > 0 ||
               RegentCharacterStrategy.IsEngineCard(card) ||
               card.EffectiveCost == 0 ||
               IsCardIdLikeAny(card,
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

    private static bool IsFrostCard(ResolvedCardView card)
    {
        return IsCardIdLikeAny(card,
            "FROST",
            "COLD_SNAP",
            "COOLHEADED",
            "COOL_HEADED",
            "GLACIER",
            "CHARGE_BATTERY",
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

    private static bool IsCardIdLikeAny(ResolvedCardView card, params string[] tokens)
    {
        return tokens.Any(token => IsCardIdLike(card, token));
    }

    private static bool IsCardIdLike(ResolvedCardView card, string token)
    {
        string normalizedId = card.CardId.Replace(' ', '_').Replace('-', '_').ToUpperInvariant();
        string normalizedName = card.Name.Replace(' ', '_').Replace('-', '_').ToUpperInvariant();
        return normalizedId.Contains(token, StringComparison.Ordinal) ||
               normalizedName.Contains(token, StringComparison.Ordinal);
    }
}
