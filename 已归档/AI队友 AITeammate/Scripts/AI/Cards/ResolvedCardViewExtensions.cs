using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AITeammate.Scripts;

internal static class ResolvedCardViewExtensions
{
    public static string GetNormalizedSearchToken(this ResolvedCardView? card)
    {
        if (card == null)
        {
            return string.Empty;
        }

        return NormalizeSearchToken($"{card.CardId} {card.Name}");
    }

    public static bool MatchesCardToken(this ResolvedCardView? card, params string[] tokens)
    {
        string normalized = card.GetNormalizedSearchToken();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        return tokens.Any(token =>
            normalized.Contains(NormalizeSearchToken(token), StringComparison.Ordinal));
    }

    public static string NormalizeSearchToken(string raw)
    {
        Span<char> buffer = raw.Length <= 512 ? stackalloc char[raw.Length] : new char[raw.Length];
        int length = 0;
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[length++] = char.ToUpperInvariant(c);
            }
            else if (length == 0 || buffer[length - 1] != '_')
            {
                buffer[length++] = '_';
            }
        }

        return new string(buffer[..length]);
    }

    public static bool HasEffect(this ResolvedCardView? card, EffectKind kind)
    {
        return card?.Effects.Any(effect => effect.Kind == kind) == true;
    }

    public static int GetReplayMultiplier(this ResolvedCardView? card)
    {
        return card == null ? 1 : 1 + Math.Max(card.ReplayCount, 0);
    }

    public static int GetEstimatedDamage(this ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int replayMultiplier = card.GetReplayMultiplier();
        return card.Effects
            .Where(static effect => effect.Kind == EffectKind.DealDamage)
            .Sum(effect => Math.Max(effect.Amount, 0) * Math.Max(effect.RepeatCount, 1) * replayMultiplier);
    }

    public static int GetEstimatedDamageWithOrbEvoke(
        this ResolvedCardView? card,
        Player? player,
        int availableEnergy = 0,
        int energyCost = 0)
    {
        return card.GetEstimatedDamage() + card.EstimateOrbEvoke(player, availableEnergy, energyCost).Damage;
    }

    public static bool DealsDamageToAllEnemies(this ResolvedCardView? card)
    {
        return card?.Effects.Any(static effect =>
            effect.Kind == EffectKind.DealDamage &&
            effect.TargetScope == TargetScope.AllEnemies) == true;
    }

    public static int GetEstimatedBlock(this ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int replayMultiplier = card.GetReplayMultiplier();
        return card.Effects
            .Where(static effect => effect.Kind == EffectKind.GainBlock)
            .Sum(effect => Math.Max(effect.Amount, 0) * Math.Max(effect.RepeatCount, 1) * replayMultiplier);
    }

    public static int GetSummonAmount(this ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int replayMultiplier = card.GetReplayMultiplier();
        return card.Effects
            .Where(static effect => effect.Kind == EffectKind.Summon)
            .Sum(effect => Math.Max(effect.Amount, 0) * Math.Max(effect.RepeatCount, 1) * replayMultiplier);
    }

    public static int GetEstimatedProtection(this ResolvedCardView? card)
    {
        return card.GetEstimatedBlock() + card.GetSummonAmount();
    }

    public static int GetEstimatedBlockWithOrbEvoke(
        this ResolvedCardView? card,
        Player? player,
        int availableEnergy = 0,
        int energyCost = 0)
    {
        return card.GetEstimatedBlock() + card.EstimateOrbEvoke(player, availableEnergy, energyCost).Block;
    }

    public static int GetEffectAmount(this ResolvedCardView? card, EffectKind kind)
    {
        if (card == null)
        {
            return 0;
        }

        int replayMultiplier = card.GetReplayMultiplier();
        return card.Effects
            .Where(effect => effect.Kind == kind)
            .Sum(effect => Math.Max(effect.Amount, 0) * Math.Max(effect.RepeatCount, 1) * replayMultiplier);
    }

    public static int GetAppliedPowerAmount(this ResolvedCardView? card, string powerId, TargetScope? targetScope = null, DurationHint? durationHint = null)
    {
        if (card == null)
        {
            return 0;
        }

        int replayMultiplier = card.GetReplayMultiplier();
        return card.Effects
            .Where(effect => effect.Kind == EffectKind.ApplyPower &&
                             string.Equals(effect.AppliedPowerId, powerId, StringComparison.Ordinal) &&
                             (!targetScope.HasValue || effect.TargetScope == targetScope.Value) &&
                             (!durationHint.HasValue || effect.DurationHint == durationHint.Value))
            .Sum(effect => Math.Max(effect.Amount, 0) * Math.Max(effect.RepeatCount, 1) * replayMultiplier);
    }

    public static bool AppliesPower(this ResolvedCardView? card, string powerId)
    {
        return card.GetAppliedPowerAmount(powerId) > 0;
    }

    public static int GetEnemyVulnerableAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Vulnerable", TargetScope.SingleEnemy) +
               card.GetAppliedPowerAmount("Vulnerable", TargetScope.AllEnemies);
    }

    public static bool AppliesVulnerableToAllEnemies(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Vulnerable", TargetScope.AllEnemies) > 0;
    }

    public static int GetEnemyWeakAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Weak", TargetScope.SingleEnemy) +
               card.GetAppliedPowerAmount("Weak", TargetScope.AllEnemies);
    }

    public static bool AppliesWeakToAllEnemies(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Weak", TargetScope.AllEnemies) > 0;
    }

    public static int GetEnemyPoisonAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Poison", TargetScope.SingleEnemy) +
               card.GetAppliedPowerAmount("Poison", TargetScope.AllEnemies);
    }

    public static bool AppliesPoisonToAllEnemies(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Poison", TargetScope.AllEnemies) > 0;
    }

    public static bool IsMultiplayerOnlyCard(this ResolvedCardView? card)
    {
        return card?.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly;
    }

    public static int GetSelfStrengthAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Strength", TargetScope.Self);
    }

    public static int GetSelfDexterityAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Dexterity", TargetScope.Self);
    }

    public static int GetSelfTemporaryStrengthAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Strength", TargetScope.Self, DurationHint.ThisTurn);
    }

    public static int GetSelfTemporaryDexterityAmount(this ResolvedCardView? card)
    {
        return card.GetAppliedPowerAmount("Dexterity", TargetScope.Self, DurationHint.ThisTurn);
    }

    public static int GetCardsDrawn(this ResolvedCardView? card)
    {
        if (RegentCharacterStrategy.TransformsDrawPileWithoutDrawing(card))
        {
            return 0;
        }

        return card.GetEffectAmount(EffectKind.DrawCards);
    }

    public static int GetEnergyGain(this ResolvedCardView? card)
    {
        return card.GetEffectAmount(EffectKind.GainEnergy);
    }

    public static int GetEnergyGainWithOrbEvoke(
        this ResolvedCardView? card,
        Player? player,
        int availableEnergy = 0,
        int energyCost = 0)
    {
        return card.GetEnergyGain() + card.EstimateOrbEvoke(player, availableEnergy, energyCost).Energy;
    }

    public static int GetStarsGenerated(this ResolvedCardView? card)
    {
        return card.GetEffectAmount(EffectKind.GainStars);
    }

    public static int GetRecognizedUtilityAmount(this ResolvedCardView? card)
    {
        return card.GetEffectAmount(EffectKind.SpecialUtility) +
               card.GetEffectAmount(EffectKind.ChannelOrb) * 3 +
               card.GetEffectAmount(EffectKind.EvokeOrb) * 4 +
               card.GetEffectAmount(EffectKind.GenerateCards) * 5 +
               card.GetEffectAmount(EffectKind.GeneratePotion) * 7 +
               card.GetEffectAmount(EffectKind.DiscardCards) * 3 +
               card.GetEffectAmount(EffectKind.ExhaustCards) * 3 +
               card.GetEffectAmount(EffectKind.UpgradeCards) * 6 +
               card.GetEffectAmount(EffectKind.RetainCards) * 3;
    }

    public static bool HasOrbSemanticEffect(this ResolvedCardView? card)
    {
        return card?.Effects.Any(static effect =>
            effect.Kind is EffectKind.ChannelOrb or EffectKind.EvokeOrb ||
            (!string.IsNullOrWhiteSpace(effect.AppliedPowerId) &&
             (effect.AppliedPowerId.Contains("Orb", StringComparison.OrdinalIgnoreCase) ||
              effect.AppliedPowerId.Contains("Focus", StringComparison.OrdinalIgnoreCase)))) == true;
    }

    public static int GetDirectDamageHits(this ResolvedCardView? card)
    {
        if (card == null)
        {
            return 0;
        }

        int replayMultiplier = card.GetReplayMultiplier();
        return card.Effects
            .Where(static effect => effect.Kind == EffectKind.DealDamage)
            .Sum(effect => Math.Max(effect.RepeatCount, 1) * replayMultiplier);
    }

    public static OrbEvokeEstimate EstimateOrbEvoke(
        this ResolvedCardView? card,
        Player? player,
        int availableEnergy = 0,
        int energyCost = 0)
    {
        if (card == null || player?.PlayerCombatState == null)
        {
            return OrbEvokeEstimate.Empty;
        }

        EvokePattern pattern = GetEvokePattern(card, availableEnergy, energyCost);
        if (!pattern.HasValue)
        {
            return OrbEvokeEstimate.Empty;
        }

        List<object> orbs = GetCurrentOrbs(player).ToList();
        if (orbs.Count == 0)
        {
            return OrbEvokeEstimate.Empty;
        }

        int damage = 0;
        int block = 0;
        int energy = 0;
        int damageHits = 0;
        bool randomEnemyDamage = false;

        if (pattern.EvokesAllOrbs)
        {
            foreach (object orb in orbs)
            {
                AddOrbValue(orb, 1, ref damage, ref block, ref energy, ref damageHits, ref randomEnemyDamage);
            }
        }
        else
        {
            AddOrbValue(orbs[0], pattern.FrontOrbRepeatCount, ref damage, ref block, ref energy, ref damageHits, ref randomEnemyDamage);
        }

        return new OrbEvokeEstimate(
            Damage: Math.Max(0, damage),
            Block: Math.Max(0, block),
            Energy: Math.Max(0, energy),
            DamageHits: Math.Max(0, damageHits),
            HasRandomEnemyDamage: randomEnemyDamage);
    }

    private static EvokePattern GetEvokePattern(ResolvedCardView card, int availableEnergy, int energyCost)
    {
        string token = card.GetNormalizedSearchToken();
        if (token.Contains("FISSION", StringComparison.Ordinal))
        {
            return EvokePattern.AllOrbs;
        }

        if (token.Contains("MULTICAST", StringComparison.Ordinal) ||
            token.Contains("MULTI_CAST", StringComparison.Ordinal))
        {
            int x = Math.Max(Math.Max(energyCost, availableEnergy), 1);
            return EvokePattern.FrontOrb(Math.Min(x, 12));
        }

        if (token.Contains("DUALCAST", StringComparison.Ordinal) ||
            token.Contains("DUAL_CAST", StringComparison.Ordinal))
        {
            return EvokePattern.FrontOrb(2);
        }

        if (token.Contains("RECURSION", StringComparison.Ordinal) ||
            token.Contains("EVOKE_NEXT", StringComparison.Ordinal) ||
            token.Contains("EVOKE", StringComparison.Ordinal))
        {
            return EvokePattern.FrontOrb(1);
        }

        return EvokePattern.None;
    }

    private static IEnumerable<object> GetCurrentOrbs(Player player)
    {
        object? combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            yield break;
        }

        object? orbs = ReadMember(combatState, "Orbs") ?? ReadMember(combatState, "OrbQueue");
        if (orbs != null && !(orbs is string))
        {
            object? nestedOrbs = ReadMember(orbs, "Orbs");
            if (nestedOrbs is IEnumerable nestedEnumerable && nestedOrbs is not string)
            {
                orbs = nestedEnumerable;
            }
        }

        if (orbs is not IEnumerable enumerable || orbs is string)
        {
            yield break;
        }

        foreach (object? orb in enumerable)
        {
            if (orb != null)
            {
                yield return orb;
            }
        }
    }

    private static void AddOrbValue(
        object orb,
        int repeats,
        ref int damage,
        ref int block,
        ref int energy,
        ref int damageHits,
        ref bool randomEnemyDamage)
    {
        int evokeVal = Math.Max(0, ReadIntMember(orb, "EvokeVal"));
        if (evokeVal <= 0 || repeats <= 0)
        {
            return;
        }

        string token = BuildOrbToken(orb);
        if (token.Contains("LIGHTNING", StringComparison.Ordinal))
        {
            damage += evokeVal * repeats;
            damageHits += repeats;
            randomEnemyDamage = true;
        }
        else if (token.Contains("DARK", StringComparison.Ordinal))
        {
            damage += evokeVal * repeats;
            damageHits += repeats;
        }
        else if (token.Contains("FROST", StringComparison.Ordinal))
        {
            block += evokeVal * repeats;
        }
        else if (token.Contains("PLASMA", StringComparison.Ordinal))
        {
            energy += evokeVal * repeats;
        }
    }

    private static string BuildOrbToken(object orb)
    {
        string id = ReadStringMember(ReadMember(orb, "Id"), "Entry") ??
                    ReadStringMember(orb, "Id") ??
                    string.Empty;
        string title = ReadStringMember(orb, "Title") ?? string.Empty;
        return $"{orb.GetType().Name} {id} {title}".ToUpperInvariant();
    }

    private static object? ReadMember(object instance, string memberName)
    {
        Type type = instance.GetType();
        return type.GetProperty(memberName)?.GetValue(instance) ??
               type.GetField(memberName)?.GetValue(instance);
    }

    private static int ReadIntMember(object instance, string memberName)
    {
        object? value = ReadMember(instance, memberName);
        if (value == null)
        {
            return 0;
        }

        try
        {
            return (int)Math.Round(Convert.ToDecimal(value));
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (InvalidCastException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    private static string? ReadStringMember(object? instance, string? memberName = null)
    {
        if (instance == null)
        {
            return null;
        }

        object? value = memberName == null ? instance : ReadMember(instance, memberName);
        return value?.ToString();
    }

    private readonly record struct EvokePattern(bool EvokesAllOrbs, int FrontOrbRepeatCount)
    {
        public bool HasValue => EvokesAllOrbs || FrontOrbRepeatCount > 0;

        public static EvokePattern None => new(false, 0);

        public static EvokePattern AllOrbs => new(true, 0);

        public static EvokePattern FrontOrb(int repeatCount)
        {
            return new EvokePattern(false, Math.Max(0, repeatCount));
        }
    }
}

internal readonly record struct OrbEvokeEstimate(
    int Damage,
    int Block,
    int Energy,
    int DamageHits,
    bool HasRandomEnemyDamage)
{
    public static OrbEvokeEstimate Empty => new(0, 0, 0, 0, false);

    public bool HasAnyBenefit => Damage > 0 || Block > 0 || Energy > 0;
}
