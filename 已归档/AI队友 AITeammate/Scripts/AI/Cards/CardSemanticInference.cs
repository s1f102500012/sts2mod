using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal static class CardSemanticInference
{
    private static readonly string[] IgnoredDamageVarTokens = ["SELF_DAMAGE", "LOSE_HP", "HP_LOSS"];

    public static int GetDamageLikeAmount(IReadOnlyDictionary<string, int> dynamicVars)
    {
        int explicitDamage = GetDynamicVar(dynamicVars, "CalculatedDamage");
        if (explicitDamage <= 0)
        {
            explicitDamage = GetDynamicVar(dynamicVars, "Damage") + GetDynamicVar(dynamicVars, "ExtraDamage");
        }

        int genericDamage = dynamicVars
            .Where(static pair => IsDamageLikeVar(pair.Key))
            .Select(static pair => Math.Max(pair.Value, 0))
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(explicitDamage, genericDamage);
    }

    public static int GetBlockLikeAmount(IReadOnlyDictionary<string, int> dynamicVars)
    {
        int explicitBlock = GetDynamicVar(dynamicVars, "CalculatedBlock");
        if (explicitBlock <= 0)
        {
            explicitBlock = GetDynamicVar(dynamicVars, "Block");
        }

        int genericBlock = dynamicVars
            .Where(static pair => IsBlockLikeVar(pair.Key))
            .Select(static pair => Math.Max(pair.Value, 0))
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(explicitBlock, genericBlock);
    }

    public static void Augment(
        List<NormalizedEffectDescriptor> effects,
        string cardId,
        string cardName,
        CardType cardType,
        string rarity,
        TargetType targetType,
        IReadOnlyDictionary<string, int> dynamicVars,
        string description)
    {
        string token = Normalize($"{cardId}_{cardName}_{description}");

        AddStatusBurden(effects, rarity, token);
        if (rarity is "Status" or "Curse")
        {
            return;
        }

        AddGenericPowerVars(effects, targetType, dynamicVars, token);
        AddCalculatedPowerVars(effects, targetType, dynamicVars, token);
        AddStrengthLoss(effects, targetType, dynamicVars, token);
        AddPoisonFallback(effects, cardType, targetType, dynamicVars, token);
        AddOrbEffects(effects, targetType, token);
        AddUtilityEffects(effects, token);
        AddKnownCardFallbacks(effects, cardId, cardType, targetType, dynamicVars, token);
        AddGenericPowerSetup(effects, cardId, cardType, targetType);

        if (effects.Count == 0 && cardType is CardType.Attack or CardType.Skill or CardType.Power)
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 0, "Unclassified");
        }
    }

    private static void AddStatusBurden(List<NormalizedEffectDescriptor> effects, string rarity, string token)
    {
        if (rarity is not ("Status" or "Curse") &&
            !ContainsWholeTokenAny(token, "ASCENDER", "ASCENDERS", "BECKON", "BURN", "CLUMSY", "CURSE", "DAZED", "INJURY", "NORMALITY", "REGRET", "SHAME", "VOID", "WOUND"))
        {
            return;
        }

        int amount = rarity == "Curse" ? 64 : 46;
        if (ContainsWholeTokenAny(token, "BECKON"))
        {
            amount = Math.Max(amount, 62);
        }
        else if (ContainsWholeTokenAny(token, "VOID", "NORMALITY", "REGRET", "SHAME"))
        {
            amount = Math.Max(amount, 32);
        }
        else if (ContainsWholeTokenAny(token, "BURN", "WOUND", "DAZED"))
        {
            amount = Math.Max(amount, 24);
        }

        AddEffect(effects, EffectKind.StatusBurden, TargetScope.Self, amount, "Status");
    }

    private static void AddGenericPowerVars(
        List<NormalizedEffectDescriptor> effects,
        TargetType targetType,
        IReadOnlyDictionary<string, int> dynamicVars,
        string token)
    {
        foreach (KeyValuePair<string, int> pair in dynamicVars)
        {
            if (pair.Value <= 0 || !pair.Key.EndsWith("Power", StringComparison.Ordinal))
            {
                continue;
            }

            string powerId = pair.Key[..^"Power".Length];
            if (string.IsNullOrWhiteSpace(powerId))
            {
                powerId = pair.Key;
            }

            AddPowerIfMissing(effects, GuessPowerTargetScope(targetType, powerId, token), pair.Value, powerId);
        }
    }

    private static void AddCalculatedPowerVars(
        List<NormalizedEffectDescriptor> effects,
        TargetType targetType,
        IReadOnlyDictionary<string, int> dynamicVars,
        string token)
    {
        int calculatedFocus = GetDynamicVar(dynamicVars, "CalculatedFocus");
        if (calculatedFocus > 0 || ContainsAny(token, "SYNCHRONIZE"))
        {
            AddPowerIfMissing(effects, TargetScope.Self, Math.Max(1, calculatedFocus), "Synchronize");
        }

        int calculatedDoom = GetDynamicVar(dynamicVars, "CalculatedDoom");
        if (calculatedDoom > 0 || ContainsAny(token, "NO_ESCAPE"))
        {
            AddPowerIfMissing(effects, MapTargetScope(targetType), Math.Max(1, calculatedDoom), "Doom");
        }

        int forge = GetDynamicVar(dynamicVars, "Forge");
        if (forge > 0)
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, forge, "Forge", DurationHint.Persistent, ValueTiming.Setup);
        }
    }

    private static void AddStrengthLoss(
        List<NormalizedEffectDescriptor> effects,
        TargetType targetType,
        IReadOnlyDictionary<string, int> dynamicVars,
        string token)
    {
        int strengthLoss = GetDynamicVar(dynamicVars, "StrengthLoss");
        strengthLoss = Math.Max(strengthLoss, GetDynamicVar(dynamicVars, "EnemyStrengthLoss"));
        if (strengthLoss <= 0 && ContainsAny(token, "ENFEEBLING_TOUCH", "DARK_SHACKLES"))
        {
            strengthLoss = 6;
        }
        else if (strengthLoss <= 0 && ContainsAny(token, "MALAISE"))
        {
            strengthLoss = 1;
        }

        if (strengthLoss > 0)
        {
            AddPowerIfMissing(effects, GuessPowerTargetScope(targetType, "StrengthLoss", token), strengthLoss, "StrengthLoss");
        }
    }

    private static void AddPoisonFallback(
        List<NormalizedEffectDescriptor> effects,
        CardType cardType,
        TargetType targetType,
        IReadOnlyDictionary<string, int> dynamicVars,
        string token)
    {
        int poison = Math.Max(
            Math.Max(GetDynamicVar(dynamicVars, "PoisonPower"), GetDynamicVar(dynamicVars, "Poison")),
            GetDynamicVar(dynamicVars, "PoisonPerTurn"));
        int repeat = Math.Max(1, GetRepeatCount(dynamicVars));
        if (poison <= 0 && ContainsAny(token, "BOUNCING_FLASK", "DEADLY_POISON", "SNAKEBITE", "OUTBREAK", "CORROSIVE_WAVE", "NOXIOUS_FUMES", "ENVENOM"))
        {
            poison = ContainsAny(token, "BOUNCING_FLASK") ? 3 : 4;
            if (ContainsAny(token, "BOUNCING_FLASK"))
            {
                repeat = Math.Max(repeat, 3);
            }
        }

        if (poison <= 0)
        {
            return;
        }

        TargetScope scope = cardType == CardType.Power && targetType == TargetType.Self
            ? TargetScope.AllEnemies
            : MapTargetScope(targetType);
        AddPowerIfMissing(effects, scope, poison, "Poison", repeat);
    }

    private static void AddOrbEffects(List<NormalizedEffectDescriptor> effects, TargetType targetType, string token)
    {
        string? orb = InferOrbToken(token);
        if (orb != null && ContainsAny(token, "ZAP", "BALL_LIGHTNING", "COLD_SNAP", "COOLHEADED", "CHILL", "GLACIER", "CHAOS", "RAINBOW", "DARKNESS", "FUSION", "TEMPEST", "THUNDER", "HAILSTORM", "VOLTAIC", "COOLANT", "IGNITION", "LIGHTNING", "FROST", "PLASMA"))
        {
            AddEffect(effects, EffectKind.ChannelOrb, MapPositiveSelfOrAllyScope(targetType), InferOrbCount(token), orb, DurationHint.Persistent, ValueTiming.Setup);
        }

        if (ContainsAny(token, "DUALCAST", "DUAL_CAST", "MULTI_CAST", "MULTICAST", "QUADCAST", "FISSION", "RECURSION", "EVOKE"))
        {
            int amount = ContainsAny(token, "QUADCAST") ? 4 : ContainsAny(token, "DUALCAST", "DUAL_CAST") ? 2 : 1;
            AddEffect(effects, EffectKind.EvokeOrb, TargetScope.Self, amount, "FrontOrb", DurationHint.Immediate, ValueTiming.Mixed);
        }
    }

    private static void AddUtilityEffects(List<NormalizedEffectDescriptor> effects, string token)
    {
        if (ContainsAny(token, "ALCHEMIZE"))
        {
            AddEffect(effects, EffectKind.GeneratePotion, TargetScope.Self, 1, "Potion", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (ContainsAny(token, "DISCOVERY", "INFERNAL_BLADE", "WHITE_NOISE", "DISTRACTION", "CREATIVE_AI", "HELLO_WORLD", "HIDDEN_GEM", "TRASH_TO_TREASURE", "SECRET_TECHNIQUE", "SECRET_WEAPON", "MIMIC"))
        {
            AddEffect(effects, EffectKind.GenerateCards, TargetScope.Self, 1, "Card", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (ContainsAny(token, "CALCULATED_GAMBLE", "PREPARED", "ACROBATICS", "SURVIVOR", "TOOLS_OF_THE_TRADE", "STORM_OF_STEEL", "DISCARD"))
        {
            AddEffect(effects, EffectKind.DiscardCards, TargetScope.Self, 1, "Discard", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (ContainsAny(token, "TRUE_GRIT", "BURNING_PACT", "SECOND_WIND", "SEVER_SOUL", "FIEND_FIRE", "HAVOC", "RECYCLE", "EXHAUST"))
        {
            AddEffect(effects, EffectKind.ExhaustCards, TargetScope.Self, 1, "Exhaust", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (ContainsAny(token, "ARMAMENT", "APOTHEOSIS", "THE_SMITH", "ARSENAL"))
        {
            AddEffect(effects, EffectKind.UpgradeCards, TargetScope.Self, 1, "Upgrade", DurationHint.Persistent, ValueTiming.Setup);
        }

        if (ContainsAny(token, "WELL_LAID_PLANS", "PREP_TIME", "RETAIN"))
        {
            AddEffect(effects, EffectKind.RetainCards, TargetScope.Self, 1, "Retain", DurationHint.Persistent, ValueTiming.Setup);
        }
    }

    private static void AddKnownCardFallbacks(
        List<NormalizedEffectDescriptor> effects,
        string cardId,
        CardType cardType,
        TargetType targetType,
        IReadOnlyDictionary<string, int> dynamicVars,
        string token)
    {
        if (IsCard(cardId, "BYRDONIS_EGG"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 55, "QuestRestSiteHatch", DurationHint.Persistent, ValueTiming.Setup);
        }

        if (IsCard(cardId, "LANTERN_KEY"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 38, "QuestForcedEvent", DurationHint.Persistent, ValueTiming.Setup);
        }

        if (IsCard(cardId, "SPOILS_MAP"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, Math.Max(120, GetDynamicVar(dynamicVars, "Gold") / 4), "QuestGoldMap", DurationHint.Persistent, ValueTiming.Setup);
        }

        if (IsCard(cardId, "MALAISE"))
        {
            AddPowerIfMissing(effects, TargetScope.SingleEnemy, 1, "Weak");
            AddPowerIfMissing(effects, TargetScope.SingleEnemy, 1, "StrengthLoss");
        }

        if (IsCard(cardId, "DOUBLE_ENERGY"))
        {
            AddEffect(effects, EffectKind.GainEnergy, TargetScope.Self, 2, "Energy", DurationHint.Immediate, ValueTiming.Immediate);
        }

        if (IsCard(cardId, "SCRAWL"))
        {
            AddEffect(effects, EffectKind.DrawCards, TargetScope.Self, 7, "Draw", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "BULLET_TIME"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 32, "FreeHandNoDraw", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "BURST"))
        {
            AddPowerIfMissing(effects, TargetScope.Self, Math.Max(1, GetDynamicVar(dynamicVars, "Skills")), "Burst");
        }

        if (IsCard(cardId, "NIGHTMARE"))
        {
            AddPowerIfMissing(effects, TargetScope.Self, 3, "Nightmare");
        }

        if (IsCard(cardId, "PROLONG"))
        {
            AddPowerIfMissing(effects, TargetScope.Self, 1, "BlockNextTurn");
        }

        if (IsCard(cardId, "WISH"))
        {
            AddEffect(effects, EffectKind.GenerateCards, TargetScope.Self, 1, "TutorDrawPile", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "QUASAR", "SPLASH", "LARGESSE"))
        {
            AddEffect(effects, EffectKind.GenerateCards, MapPositiveSelfOrAllyScope(targetType), 1, "GeneratedChoice", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "CHARGE", "PRIMAL_FORCE"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 24, "TransformCards", DurationHint.Persistent, ValueTiming.Setup);
        }

        if (IsCard(cardId, "STOKE"))
        {
            AddEffect(effects, EffectKind.ExhaustCards, TargetScope.Self, 3, "ExhaustHand", DurationHint.Immediate, ValueTiming.Mixed);
            AddEffect(effects, EffectKind.GenerateCards, TargetScope.Self, 3, "RefillHand", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "SUMMON_FORTH"))
        {
            AddEffect(effects, EffectKind.GenerateCards, TargetScope.Self, 1, "SovereignBlade", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "CASCADE"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 28, "AutoplayDrawPile", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (IsCard(cardId, "ENTRENCH", "STACK", "MIRAGE", "SACRIFICE"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 18, cardId, DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (IsCard(cardId, "ANOINTED"))
        {
            AddEffect(effects, EffectKind.GenerateCards, TargetScope.Self, 2, "TutorRareDrawPile", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "BEGONE"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 34, "TransformHandToMinionStrike", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (IsCard(cardId, "DEMONIC_SHIELD"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.SingleAlly, 26, "AllyBlockFromCurrentBlock", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (IsCard(cardId, "EIDOLON"))
        {
            AddEffect(effects, EffectKind.ExhaustCards, TargetScope.Self, 5, "ExhaustHand", DurationHint.Immediate, ValueTiming.Mixed);
            AddPowerIfMissing(effects, TargetScope.Self, 1, "Intangible");
        }

        if (IsCard(cardId, "ENLIGHTENMENT"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 30, "HandCostReduction", DurationHint.Immediate, ValueTiming.Setup);
        }

        if (IsCard(cardId, "EXPECT_A_FIGHT"))
        {
            AddEffect(effects, EffectKind.GainEnergy, TargetScope.Self, Math.Max(2, GetDynamicVar(dynamicVars, "CalculatedEnergy")), "Energy", DurationHint.Immediate, ValueTiming.Immediate);
            AddPowerIfMissing(effects, TargetScope.Self, 1, "NoEnergyGain");
        }

        if (IsCard(cardId, "FLANKING"))
        {
            AddPowerIfMissing(effects, TargetScope.SingleEnemy, 2, "Flanking");
        }

        if (IsCard(cardId, "GUARDS"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, 30, "TransformHandToMinionSacrifice", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (IsCard(cardId, "KNIFE_TRAP"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.SingleEnemy, 30, "AutoplayExhaustedShivs", DurationHint.Immediate, ValueTiming.Mixed);
        }

        if (IsCard(cardId, "NOT_YET"))
        {
            AddEffect(effects, EffectKind.SpecialUtility, TargetScope.Self, Math.Max(10, GetDynamicVar(dynamicVars, "Heal")), "Heal", DurationHint.Immediate, ValueTiming.Immediate);
        }

        if (IsCard(cardId, "ONE_TWO_PUNCH"))
        {
            AddPowerIfMissing(effects, TargetScope.Self, Math.Max(1, GetDynamicVar(dynamicVars, "Attacks")), "OneTwoPunch");
        }
    }

    private static void AddGenericPowerSetup(List<NormalizedEffectDescriptor> effects, string cardId, CardType cardType, TargetType targetType)
    {
        if (cardType != CardType.Power ||
            effects.Any(static effect => effect.Kind == EffectKind.ApplyPower && effect.TargetScope == TargetScope.Self))
        {
            return;
        }

        AddPowerIfMissing(effects, MapTargetScope(targetType), 1, cardId);
    }

    private static int GetRepeatCount(IReadOnlyDictionary<string, int> dynamicVars)
    {
        int repeat = GetDynamicVar(dynamicVars, "Repeat");
        foreach (KeyValuePair<string, int> pair in dynamicVars)
        {
            if (pair.Key.Contains("Repeat", StringComparison.OrdinalIgnoreCase) &&
                !pair.Key.Contains("CannotRepeat", StringComparison.OrdinalIgnoreCase))
            {
                repeat = Math.Max(repeat, pair.Value);
            }
        }

        return Math.Max(repeat, 1);
    }

    private static bool IsDamageLikeVar(string key)
    {
        string token = Normalize(key);
        return token.Contains("DAMAGE", StringComparison.Ordinal) &&
               !IgnoredDamageVarTokens.Any(token.Contains);
    }

    private static bool IsBlockLikeVar(string key)
    {
        string token = Normalize(key);
        return token.Contains("BLOCK", StringComparison.Ordinal) ||
               token.Contains("ARMOR", StringComparison.Ordinal);
    }

    private static int GetDynamicVar(IReadOnlyDictionary<string, int> dynamicVars, string name)
    {
        return dynamicVars.TryGetValue(name, out int value) ? Math.Max(value, 0) : 0;
    }

    private static string? InferOrbToken(string token)
    {
        if (ContainsAny(token, "FROST", "CHILL", "COOLHEADED", "COOLANT", "GLACIER", "COLD_SNAP"))
        {
            return "FrostOrb";
        }

        if (ContainsAny(token, "DARKNESS", "DARK_ORB"))
        {
            return "DarkOrb";
        }

        if (ContainsAny(token, "FUSION", "PLASMA"))
        {
            return "PlasmaOrb";
        }

        if (ContainsAny(token, "IGNITION"))
        {
            return "PlasmaOrb";
        }

        if (ContainsAny(token, "RAINBOW", "CHAOS"))
        {
            return "MixedOrb";
        }

        if (ContainsAny(token, "ZAP", "BALL_LIGHTNING", "THUNDER", "HAILSTORM", "VOLTAIC", "LIGHTNING", "TEMPEST"))
        {
            return "LightningOrb";
        }

        return null;
    }

    private static int InferOrbCount(string token)
    {
        if (ContainsAny(token, "RAINBOW"))
        {
            return 3;
        }

        if (ContainsAny(token, "TEMPEST"))
        {
            return 2;
        }

        return 1;
    }

    private static void AddPowerIfMissing(
        List<NormalizedEffectDescriptor> effects,
        TargetScope targetScope,
        int amount,
        string powerId,
        int repeatCount = 1)
    {
        if (effects.Any(effect =>
                effect.Kind == EffectKind.ApplyPower &&
                string.Equals(effect.AppliedPowerId, powerId, StringComparison.Ordinal) &&
                effect.TargetScope == targetScope))
        {
            return;
        }

        AddEffect(effects, EffectKind.ApplyPower, targetScope, amount, powerId, DurationHint.Unknown, ValueTiming.Mixed, repeatCount);
    }

    private static void AddEffect(
        List<NormalizedEffectDescriptor> effects,
        EffectKind kind,
        TargetScope targetScope,
        int amount,
        string? appliedPowerId,
        DurationHint durationHint = DurationHint.Immediate,
        ValueTiming valueTiming = ValueTiming.Immediate,
        int repeatCount = 1)
    {
        if (effects.Any(effect =>
                effect.Kind == kind &&
                effect.TargetScope == targetScope &&
                string.Equals(effect.AppliedPowerId, appliedPowerId, StringComparison.Ordinal)))
        {
            return;
        }

        effects.Add(new NormalizedEffectDescriptor
        {
            Kind = kind,
            TargetScope = targetScope,
            Amount = Math.Max(0, amount),
            RepeatCount = Math.Max(1, repeatCount),
            AppliedPowerId = appliedPowerId,
            DurationHint = durationHint,
            ValueTiming = valueTiming
        });
    }

    private static TargetScope GuessPowerTargetScope(TargetType targetType, string powerId, string token)
    {
        if (ContainsAny(powerId, "StrengthLoss", "Weak", "Vulnerable", "Poison") ||
            ContainsAny(token, "ENEMY", "ENEMIES"))
        {
            return targetType == TargetType.AllEnemies ? TargetScope.AllEnemies : TargetScope.SingleEnemy;
        }

        return MapTargetScope(targetType);
    }

    private static TargetScope MapTargetScope(TargetType targetType)
    {
        return targetType switch
        {
            TargetType.Self => TargetScope.Self,
            TargetType.AnyEnemy or TargetType.RandomEnemy => TargetScope.SingleEnemy,
            TargetType.AllEnemies => TargetScope.AllEnemies,
            TargetType.AnyAlly or TargetType.AnyPlayer or TargetType.Osty => TargetScope.SingleAlly,
            TargetType.AllAllies => TargetScope.AllAllies,
            _ => TargetScope.Any
        };
    }

    private static TargetScope MapPositiveSelfOrAllyScope(TargetType targetType)
    {
        TargetScope scope = MapTargetScope(targetType);
        return scope is TargetScope.SingleAlly or TargetScope.AllAllies ? scope : TargetScope.Self;
    }

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token => value.Contains(Normalize(token), StringComparison.Ordinal));
    }

    private static bool ContainsWholeTokenAny(string value, params string[] tokens)
    {
        string wrapped = $"_{value}_";
        return tokens.Any(token => wrapped.Contains($"_{Normalize(token)}_", StringComparison.Ordinal));
    }

    private static bool IsCard(string cardId, params string[] cardIds)
    {
        string normalizedCardId = Normalize(cardId);
        return cardIds.Any(candidate => string.Equals(normalizedCardId, Normalize(candidate), StringComparison.Ordinal));
    }

    private static string Normalize(string raw)
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
}
