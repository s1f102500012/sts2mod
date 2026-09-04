using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class CardCatalogBuilder
{
    public CardCatalogRepository Build()
    {
        CardCatalogRepository repository = new();
        int builtCount = 0;
        int upgradedCount = 0;

        foreach (CardModel card in ModelDb.AllCards.OrderBy(static card => card.Id.Entry, StringComparer.Ordinal))
        {
            CardCatalogEntry entry = BuildEntry(card, out bool hasUpgradeInfo);
            repository.Upsert(entry);
            builtCount++;
            if (hasUpgradeInfo)
            {
                upgradedCount++;
            }
        }

        Log.Info($"[AITeammate] Built static card catalog entries={builtCount} upgraded={upgradedCount}.");
        return repository;
    }

    private static CardCatalogEntry BuildEntry(CardModel canonicalCard, out bool hasUpgradeInfo)
    {
        CardSnapshot baseSnapshot = CaptureSnapshot(canonicalCard, isUpgradePreview: false);
        CardSnapshot? upgradedSnapshot = TryCaptureFirstUpgradeSnapshot(canonicalCard);
        CardUpgradeSpec upgradeSpec = upgradedSnapshot != null
            ? BuildUpgradeSpec(baseSnapshot, upgradedSnapshot)
            : CardUpgradeSpec.Empty;

        hasUpgradeInfo = upgradedSnapshot != null;

        CardCatalogEntry entry = new()
        {
            CardId = canonicalCard.Id.Entry,
            Name = canonicalCard.Title,
            PoolId = canonicalCard.Pool.Id.Entry,
            Type = canonicalCard.Type,
            Rarity = canonicalCard.Rarity.ToString(),
            MultiplayerConstraint = canonicalCard.MultiplayerConstraint,
            TargetType = canonicalCard.TargetType,
            ShouldShowInCardLibrary = canonicalCard.ShouldShowInCardLibrary,
            BaseCost = baseSnapshot.Cost,
            HasXCost = baseSnapshot.HasXCost,
            BaseStarCost = baseSnapshot.StarCost,
            HasXStarCost = baseSnapshot.HasXStarCost,
            BaseDescription = baseSnapshot.Description,
            UpgradeDescriptionPreview = upgradedSnapshot?.Description ?? string.Empty,
            Keywords = baseSnapshot.Keywords,
            Tags = baseSnapshot.Tags,
            HoverTipRefs = baseSnapshot.HoverTipRefs,
            MaxUpgradeLevel = canonicalCard.MaxUpgradeLevel,
            BaseFlags = baseSnapshot.Flags,
            BaseDynamicVars = baseSnapshot.DynamicVars,
            UpgradeSpec = upgradeSpec,
            SemanticProfile = new CardSemanticProfile
            {
                Effects = baseSnapshot.Effects
            }
        };

        Log.Debug($"[AITeammate] Catalog card built id={entry.CardId} upgraded={(hasUpgradeInfo ? "yes" : "no")} effects=[{string.Join(", ", entry.SemanticProfile.Effects.Select(static effect => effect.Describe()))}]");
        return entry;
    }

    private static CardSnapshot CaptureSnapshot(CardModel card, bool isUpgradePreview)
    {
        string description = GetRawLocText(card.Description);
        IReadOnlyDictionary<string, int> dynamicVars = GetDynamicVars(card);
        CardFlags flags = GetFlags(card);
        IReadOnlyList<HoverTipRef> hoverTipRefs = GetHoverTipRefs(card);
        IReadOnlyList<NormalizedEffectDescriptor> effects = ExtractSemanticEffects(card, description, dynamicVars);

        return new CardSnapshot
        {
            Cost = card.EnergyCost.Canonical,
            HasXCost = card.EnergyCost.CostsX,
            StarCost = GetCurrentStarCost(card),
            HasXStarCost = card.HasStarCostX,
            Description = description,
            Keywords = card.Keywords.Select(static keyword => keyword.ToString()).OrderBy(static keyword => keyword, StringComparer.Ordinal).ToArray(),
            Tags = card.Tags.Select(static tag => tag.ToString()).OrderBy(static tag => tag, StringComparer.Ordinal).ToArray(),
            HoverTipRefs = hoverTipRefs,
            Flags = flags,
            DynamicVars = dynamicVars,
            Effects = effects
        };
    }

    private static CardSnapshot? TryCaptureFirstUpgradeSnapshot(CardModel canonicalCard)
    {
        if (canonicalCard.MaxUpgradeLevel <= 0)
        {
            return null;
        }

        CardModel upgraded = canonicalCard.ToMutable();
        upgraded.UpgradeInternal();
        upgraded.FinalizeUpgradeInternal();
        return CaptureSnapshot(upgraded, isUpgradePreview: true);
    }

    private static CardUpgradeSpec BuildUpgradeSpec(CardSnapshot baseSnapshot, CardSnapshot upgradedSnapshot)
    {
        Dictionary<EffectAdjustmentKey, int> effectAdjustments = new();

        foreach (NormalizedEffectDescriptor effect in baseSnapshot.Effects.Concat(upgradedSnapshot.Effects))
        {
            EffectAdjustmentKey key = new(effect.Kind, effect.AppliedPowerId);
            int upgradedAmount = upgradedSnapshot.Effects
                .Where(candidate => candidate.Kind == effect.Kind &&
                                    string.Equals(candidate.AppliedPowerId, effect.AppliedPowerId, StringComparison.Ordinal))
                .Sum(static candidate => Math.Max(candidate.Amount, 0));
            int baseAmount = baseSnapshot.Effects
                .Where(candidate => candidate.Kind == effect.Kind &&
                                    string.Equals(candidate.AppliedPowerId, effect.AppliedPowerId, StringComparison.Ordinal))
                .Sum(static candidate => Math.Max(candidate.Amount, 0));
            int delta = upgradedAmount - baseAmount;
            if (delta != 0)
            {
                effectAdjustments[key] = delta;
            }
        }

        int costDelta = upgradedSnapshot.Cost - baseSnapshot.Cost;
        int? costOverride = costDelta == 0 ? null : upgradedSnapshot.Cost;

        return new CardUpgradeSpec
        {
            CostDelta = 0,
            CostOverride = costOverride,
            Exhaust = baseSnapshot.Flags.Exhaust != upgradedSnapshot.Flags.Exhaust ? upgradedSnapshot.Flags.Exhaust : null,
            Ethereal = baseSnapshot.Flags.Ethereal != upgradedSnapshot.Flags.Ethereal ? upgradedSnapshot.Flags.Ethereal : null,
            Retain = baseSnapshot.Flags.Retain != upgradedSnapshot.Flags.Retain ? upgradedSnapshot.Flags.Retain : null,
            ReplayCountOverride = baseSnapshot.Flags.ReplayCount != upgradedSnapshot.Flags.ReplayCount ? upgradedSnapshot.Flags.ReplayCount : null,
            EffectAmountAdjustments = effectAdjustments
        };
    }

    private static string GetRawDescription(CardModel card)
    {
        string description = GetRawLocText(card.Description);
        return string.IsNullOrWhiteSpace(description)
            ? card.Id.Entry
            : description;
    }

    private static string GetRawLocText(LocString? locString)
    {
        if (locString == null || locString.IsEmpty)
        {
            return string.Empty;
        }

        try
        {
            string raw = locString.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }
        }
        catch (LocException)
        {
        }

        return string.IsNullOrWhiteSpace(locString.LocEntryKey)
            ? string.Empty
            : locString.LocEntryKey;
    }

    private static IReadOnlyDictionary<string, int> GetDynamicVars(CardModel card)
    {
        Dictionary<string, int> values = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, DynamicVar> pair in card.DynamicVars)
        {
            values[pair.Key] = (int)pair.Value.BaseValue;
        }

        return values;
    }

    private static CardFlags GetFlags(CardModel card)
    {
        IReadOnlySet<CardKeyword> keywords = card.Keywords;
        return new CardFlags
        {
            Exhaust = keywords.Contains(CardKeyword.Exhaust),
            Ethereal = keywords.Contains(CardKeyword.Ethereal),
            Retain = keywords.Contains(CardKeyword.Retain),
            Innate = keywords.Contains(CardKeyword.Innate),
            Unplayable = keywords.Contains(CardKeyword.Unplayable),
            ReplayCount = Math.Max(card.BaseReplayCount, 0)
        };
    }

    private static IReadOnlyList<HoverTipRef> GetHoverTipRefs(CardModel card)
    {
        List<HoverTipRef> refs = [];
        foreach (IHoverTip tip in card.HoverTips)
        {
            string refId = tip.CanonicalModel?.Id.Entry
                           ?? tip.Id
                           ?? GetHoverTipTitle(tip)
                           ?? GetHoverTipDescription(tip)
                           ?? "unknown";
            refs.Add(new HoverTipRef
            {
                Kind = GetHoverTipKind(tip),
                RefId = refId,
                Title = GetHoverTipTitle(tip),
                Description = GetHoverTipDescription(tip)
            });
        }

        return refs;
    }

    private static HoverTipRefKind GetHoverTipKind(IHoverTip tip)
    {
        if (tip is CardHoverTip)
        {
            return HoverTipRefKind.Card;
        }

        return tip.CanonicalModel switch
        {
            PowerModel => HoverTipRefKind.Power,
            CardModel => HoverTipRefKind.Card,
            OrbModel => HoverTipRefKind.Orb,
            _ => InferHoverTipKindFromId(tip.Id)
        };
    }

    private static HoverTipRefKind InferHoverTipKindFromId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return HoverTipRefKind.Unknown;
        }

        if (id.Contains("static_hover_tips", StringComparison.OrdinalIgnoreCase))
        {
            return HoverTipRefKind.StaticConcept;
        }

        if (id.Contains("card_keywords", StringComparison.OrdinalIgnoreCase))
        {
            return HoverTipRefKind.Keyword;
        }

        if (id.Contains("POWER", StringComparison.OrdinalIgnoreCase))
        {
            return HoverTipRefKind.Power;
        }

        return HoverTipRefKind.Unknown;
    }

    private static string? GetHoverTipTitle(IHoverTip tip)
    {
        return tip switch
        {
            HoverTip hoverTip => hoverTip.Title,
            CardHoverTip cardHoverTip => cardHoverTip.Card.Title,
            _ => tip.CanonicalModel switch
            {
                CardModel cardModel => cardModel.Title,
                PowerModel powerModel => GetRawLocText(powerModel.Title),
                OrbModel orbModel => GetRawLocText(orbModel.Title),
                _ => null
            }
        };
    }

    private static string? GetHoverTipDescription(IHoverTip tip)
    {
        return tip switch
        {
            HoverTip hoverTip => hoverTip.Description,
            CardHoverTip cardHoverTip => GetRawDescription(cardHoverTip.Card),
            _ => tip.CanonicalModel switch
            {
                PowerModel powerModel => GetRawLocText(powerModel.Description),
                _ => null
            }
        };
    }

    private static IReadOnlyList<NormalizedEffectDescriptor> ExtractSemanticEffects(
        CardModel card,
        string description,
        IReadOnlyDictionary<string, int> dynamicVars)
    {
        List<NormalizedEffectDescriptor> effects = [];
        bool isStatusOrCurse = IsStatusOrCurse(card);
        int repeatCount = GetRepeatCount(dynamicVars);
        int damage = CardSemanticInference.GetDamageLikeAmount(dynamicVars);
        if (!isStatusOrCurse && damage > 0)
        {
            effects.Add(new NormalizedEffectDescriptor
            {
                Kind = EffectKind.DealDamage,
                TargetScope = MapTargetScope(card.TargetType),
                Amount = damage,
                RepeatCount = Math.Max(repeatCount, 1),
                DurationHint = DurationHint.Immediate,
                ValueTiming = ValueTiming.Immediate
            });
        }

        int block = CardSemanticInference.GetBlockLikeAmount(dynamicVars);
        if (block <= 0 && card.GainsBlock)
        {
            block = GetDynamicVar(dynamicVars, "CalculatedBlock");
        }

        if (!isStatusOrCurse && block > 0)
        {
            effects.Add(new NormalizedEffectDescriptor
            {
                Kind = EffectKind.GainBlock,
                TargetScope = TargetScope.Self,
                Amount = block,
                DurationHint = DurationHint.Immediate,
                ValueTiming = ValueTiming.Immediate
            });
        }

        int summon = GetDynamicVar(dynamicVars, "Summon");
        if (!isStatusOrCurse && summon > 0)
        {
            effects.Add(new NormalizedEffectDescriptor
            {
                Kind = EffectKind.Summon,
                TargetScope = TargetScope.Self,
                Amount = summon,
                DurationHint = DurationHint.Persistent,
                ValueTiming = ValueTiming.Setup
            });
        }

        if (!isStatusOrCurse)
        {
            AddApplyPowerEffect(effects, card.TargetType, description, dynamicVars, "Vulnerable", "VulnerablePower");
            AddApplyPowerEffect(effects, card.TargetType, description, dynamicVars, "Weak", "WeakPower");
            AddApplyPowerEffect(effects, card.TargetType, description, dynamicVars, "Strength", "StrengthPower");
            AddApplyPowerEffect(effects, card.TargetType, description, dynamicVars, "Dexterity", "DexterityPower");
        }

        int cardsDrawn = GetDynamicVar(dynamicVars, "Cards");
        if (IsDrawPileTransformCard(card))
        {
            cardsDrawn = 0;
        }

        if (!isStatusOrCurse && cardsDrawn > 0)
        {
            effects.Add(new NormalizedEffectDescriptor
            {
                Kind = EffectKind.DrawCards,
                TargetScope = TargetScope.Self,
                Amount = cardsDrawn,
                DurationHint = DurationHint.Immediate,
                ValueTiming = ValueTiming.Setup
            });
        }

        int energyGain = GetDynamicVar(dynamicVars, "Energy");
        if (!isStatusOrCurse && energyGain > 0 && ShouldTreatEnergyDynamicVarAsImmediate(card))
        {
            effects.Add(new NormalizedEffectDescriptor
            {
                Kind = EffectKind.GainEnergy,
                TargetScope = TargetScope.Self,
                Amount = energyGain,
                DurationHint = DurationHint.Immediate,
                ValueTiming = ValueTiming.Immediate
            });
        }

        int starsGained = GetDynamicVar(dynamicVars, "Stars");
        if (!isStatusOrCurse && starsGained > 0)
        {
            effects.Add(new NormalizedEffectDescriptor
            {
                Kind = EffectKind.GainStars,
                TargetScope = TargetScope.Self,
                Amount = starsGained,
                DurationHint = DurationHint.Immediate,
                ValueTiming = ValueTiming.Setup
            });
        }

        CardSemanticInference.Augment(
            effects,
            card.Id.Entry,
            card.Title,
            card.Type,
            card.Rarity.ToString(),
            card.TargetType,
            dynamicVars,
            description);
        return effects;
    }

    private static bool IsDrawPileTransformCard(CardModel card)
    {
        return string.Equals(card.Id.Entry, "CHARGE", StringComparison.Ordinal);
    }

    private static bool ShouldTreatEnergyDynamicVarAsImmediate(CardModel card)
    {
        string cardId = card.Id.Entry;
        return !string.Equals(cardId, "HEGEMONY", StringComparison.Ordinal) &&
               !string.Equals(cardId, "ORBIT", StringComparison.Ordinal);
    }

    private static bool IsStatusOrCurse(CardModel card)
    {
        string rarity = card.Rarity.ToString();
        return rarity is "Status" or "Curse";
    }

    private static int GetCurrentStarCost(CardModel card)
    {
        try
        {
            return card.HasStarCostX ? -1 : card.CurrentStarCost;
        }
        catch (Exception)
        {
            return card.HasStarCostX ? -1 : card.CanonicalStarCost;
        }
    }

    private static void AddApplyPowerEffect(
        List<NormalizedEffectDescriptor> effects,
        TargetType targetType,
        string description,
        IReadOnlyDictionary<string, int> dynamicVars,
        string powerId,
        string dynamicVarName)
    {
        int amount = GetDynamicVar(dynamicVars, dynamicVarName);
        if (amount <= 0)
        {
            return;
        }

        bool isTemporaryBuff = (powerId is "Strength" or "Dexterity") &&
                               description.Contains("this turn", StringComparison.OrdinalIgnoreCase);
        effects.Add(new NormalizedEffectDescriptor
        {
            Kind = EffectKind.ApplyPower,
            TargetScope = GuessPowerTargetScope(targetType, powerId),
            Amount = amount,
            AppliedPowerId = powerId,
            DurationHint = isTemporaryBuff ? DurationHint.ThisTurn : DurationHint.Unknown,
            ValueTiming = powerId switch
            {
                "Weak" or "Vulnerable" => ValueTiming.Mixed,
                "Strength" or "Dexterity" when isTemporaryBuff => ValueTiming.Mixed,
                "Strength" or "Dexterity" => ValueTiming.Setup,
                _ => ValueTiming.Setup
            }
        });
    }

    private static int GetDynamicVar(IReadOnlyDictionary<string, int> dynamicVars, string name, int fallback = 0)
    {
        return dynamicVars.TryGetValue(name, out int value) ? Math.Max(value, 0) : fallback;
    }

    private static int GetRepeatCount(IReadOnlyDictionary<string, int> dynamicVars)
    {
        int repeat = GetDynamicVar(dynamicVars, "Repeat", fallback: 1);
        foreach (KeyValuePair<string, int> dynamicVar in dynamicVars)
        {
            string key = dynamicVar.Key;
            if (!key.Contains("Repeat", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("CannotRepeat", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            repeat = Math.Max(repeat, Math.Max(dynamicVar.Value, 0));
        }

        return Math.Max(repeat, 1);
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

    private static TargetScope GuessPowerTargetScope(TargetType targetType, string powerId)
    {
        return powerId switch
        {
            "Strength" or "Dexterity" when targetType is TargetType.Self or TargetType.AnyPlayer or TargetType.AnyAlly => TargetScope.Self,
            _ => MapTargetScope(targetType)
        };
    }

    private sealed class CardSnapshot
    {
        public int Cost { get; init; }

        public bool HasXCost { get; init; }

        public int StarCost { get; init; } = -1;

        public bool HasXStarCost { get; init; }

        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<string> Keywords { get; init; } = [];

        public IReadOnlyList<string> Tags { get; init; } = [];

        public IReadOnlyList<HoverTipRef> HoverTipRefs { get; init; } = [];

        public CardFlags Flags { get; init; } = new();

        public IReadOnlyDictionary<string, int> DynamicVars { get; init; } = new Dictionary<string, int>();

        public IReadOnlyList<NormalizedEffectDescriptor> Effects { get; init; } = [];
    }
}
