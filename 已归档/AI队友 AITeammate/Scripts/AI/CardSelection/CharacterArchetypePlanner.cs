using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal static class CharacterArchetypePlanner
{
    private const double CommittedThreshold = 7.0d;
    private const double EmergingThreshold = 3.5d;

    private static readonly IReadOnlyDictionary<CharacterFamily, IReadOnlyList<ArchetypeDefinition>> Definitions =
        new Dictionary<CharacterFamily, IReadOnlyList<ArchetypeDefinition>>
        {
            [CharacterFamily.Ironclad] =
            [
                new(
                    "ironclad_exhaust_burn",
                    CoreTokens: ["EXHAUST", "BURNING_PACT", "TRUE_GRIT", "SECOND_WIND", "FIEND_FIRE", "SEVER_SOUL", "CORRUPTION"],
                    PayoffTokens: ["FEEL_NO_PAIN", "DARK_EMBRACE", "CHARON", "FIRE_BREATHING", "EVOLVE", "JUGGERNAUT"],
                    SupportTokens: ["DRAW", "OFFERING", "BATTLE_TRANCE", "POMMEL_STRIKE", "WARCRY", "WAR_CRY"],
                    AvoidTokens: []),
                new(
                    "ironclad_vulnerable",
                    CoreTokens: ["BASH", "VULNERABLE", "THUNDERCLAP", "UPPERCUT", "SHOCKWAVE", "INTIMIDATE"],
                    PayoffTokens: ["DROPKICK", "POMMEL_STRIKE", "HEAVY_BLADE", "CARNAGE", "BLUDGEON", "IMMOLATE"],
                    SupportTokens: ["DRAW", "ENERGY", "DOUBLE_TAP", "LIMIT_BREAK"],
                    AvoidTokens: []),
                new(
                    "ironclad_strength",
                    CoreTokens: ["STRENGTH", "INFLAME", "SPOT_WEAKNESS", "DEMON_FORM", "LIMIT_BREAK", "RUPTURE"],
                    PayoffTokens: ["HEAVY_BLADE", "PUMMEL", "SWORD_BOOMERANG", "REAPER", "WHIRLWIND", "TWIN_STRIKE"],
                    SupportTokens: ["VULNERABLE", "DRAW", "ENERGY", "OFFERING"],
                    AvoidTokens: []),
                new(
                    "ironclad_block_body",
                    CoreTokens: ["BARRICADE", "ENTRENCH", "FEEL_NO_PAIN", "IMPERvious", "FLAME_BARRIER", "POWER_THROUGH", "RAGE"],
                    PayoffTokens: ["BODY_SLAM", "JUGGERNAUT"],
                    SupportTokens: ["SHRUG", "SHRUG_IT_OFF", "GHOSTLY_ARMOR", "DEXTERITY", "WEAK"],
                    AvoidTokens: [])
            ],
            [CharacterFamily.Silent] =
            [
                new(
                    "silent_shiv",
                    CoreTokens: ["SHIV", "BLADE_DANCE", "CLOAK_AND_DAGGER", "DAGGER_SPRAY", "STORM_OF_STEEL"],
                    PayoffTokens: ["ACCURACY", "WRIST_BLADE", "AFTER_IMAGE", "THOUSAND_CUTS", "FINISHER", "KUNAI", "SHURIKEN"],
                    SupportTokens: ["DEXTERITY", "DRAW", "ENERGY", "TERROR", "VULNERABLE"],
                    AvoidTokens: []),
                new(
                    "silent_poison",
                    CoreTokens: ["POISON", "NOXIOUS_FUMES", "DEADLY_POISON", "POISONED_STAB", "BOUNCING_FLASK", "CRippling_CLOUD"],
                    PayoffTokens: ["CATALYST", "BURST", "CORPSE_EXPLOSION", "ENvenom"],
                    SupportTokens: ["WEAK", "BLOCK", "DRAW", "MALAISE", "PIERCING_WAIL"],
                    AvoidTokens: []),
                new(
                    "silent_discard_tactician",
                    CoreTokens: ["DISCARD", "PREPARED", "ACROBATICS", "CALCULATED_GAMBLE", "DAGGER_THROW", "TOOLS_OF_THE_TRADE"],
                    PayoffTokens: ["TACTICIAN", "REFLEX", "EVISCERATE", "SNEAKY_STRIKE", "GRAND_FINALE"],
                    SupportTokens: ["DRAW", "ENERGY", "CONCENTRATE", "WELL_LAID_PLANS", "RETAIN"],
                    AvoidTokens: []),
                new(
                    "silent_defense_control",
                    CoreTokens: ["FOOTWORK", "DEXTERITY", "WEAK", "PIERCING_WAIL", "MALAISE", "BLUR", "DODGE_AND_ROLL"],
                    PayoffTokens: ["AFTER_IMAGE", "THOUSAND_CUTS", "WRAITH_FORM"],
                    SupportTokens: ["DRAW", "RETAIN", "BACKFLIP", "LEG_SWEEP"],
                    AvoidTokens: [])
            ],
            [CharacterFamily.Regent] =
            [
                new(
                    "regent_colorless",
                    CoreTokens: ["COLORLESS", "PRISM", "DISCOVERY", "FOREIGN", "SECRET", "APOTHEOSIS", "PANACEA", "HAND_OF_GREED", "ENLIGHTENMENT"],
                    PayoffTokens: ["ARSENAL", "ORBIT", "CHILD_OF_THE_STARS", "FURNACE"],
                    SupportTokens: ["DRAW", "ENERGY", "RETAIN", "VENERATE"],
                    AvoidTokens: []),
                new(
                    "regent_starlight",
                    CoreTokens: ["STAR", "STARS", "VENERATE", "ROYAL_GAMBLE", "FALLING_STAR", "CHILD_OF_THE_STARS"],
                    PayoffTokens: ["SEVEN_STARS", "CLOAK_OF_STARS", "PARTICLE_WALL", "ASTRAL_PULSE", "STARDUST", "ORBIT"],
                    SupportTokens: ["DRAW", "ENERGY", "BLOCK"],
                    AvoidTokens: ["ROYALTIES"])
            ],
            [CharacterFamily.Defect] =
            [
                new(
                    "defect_status",
                    CoreTokens: ["STATUS", "BURN", "DAZED", "VOID", "WOUND", "STATIC", "TURBO", "OVERclock", "HELLO_WORLD"],
                    PayoffTokens: ["EVOLVE", "FIRE_BREATHING", "MEDICAL_KIT", "SCRAPE", "REBOOT"],
                    SupportTokens: ["DRAW", "ENERGY", "BLOCK", "FROST"],
                    AvoidTokens: []),
                new(
                    "defect_power",
                    CoreTokens: ["POWER", "STORM", "HEATSINK", "HEAT_SINK", "CREATIVE_AI", "MACHINE_LEARNING", "ECHO_FORM"],
                    PayoffTokens: ["STORM", "ELECTRODYNAMICS", "BUFFER", "LOOP", "AMPLIFY", "ECHO_FORM"],
                    SupportTokens: ["DRAW", "ENERGY", "FROST", "FOCUS"],
                    AvoidTokens: []),
                new(
                    "defect_orb_charge",
                    CoreTokens: ["ORB", "CHANNEL", "EVOKE", "LIGHTNING", "FROST", "DARK", "PLASMA", "ZAP", "DUALCAST", "DUAL_CAST"],
                    PayoffTokens: ["FOCUS", "DEFRAGMENT", "DE_FRAGMENT", "BIASED_COGNITION", "CONSUME", "CAPACITOR", "LOOP", "MULTI_CAST", "ELECTRODYNAMICS"],
                    SupportTokens: ["COOLHEADED", "COOL_HEADED", "GLACIER", "RECURSION", "HOLOGRAM", "CHARGE_BATTERY"],
                    AvoidTokens: []),
                new(
                    "defect_zero_cost_engine",
                    CoreTokens: ["ALL_FOR_ONE", "SCRAPE", "REBOOT", "HOLOGRAM", "TURBO", "DOUBLE_ENERGY", "ENERGY_SURGE", "ITERATION"],
                    PayoffTokens: ["CLAW", "GO_FOR_THE_EYES", "BEAM_CELL", "FTL", "STREAMLINE"],
                    SupportTokens: ["DRAW", "ENERGY", "ZERO", "0_COST"],
                    AvoidTokens: [])
            ],
            [CharacterFamily.Necrobinder] =
            [
                new(
                    "necrobinder_summon",
                    CoreTokens: ["SUMMON", "MINION", "SKELETON", "SERVANT", "FAMILIAR", "THRALL", "REANIMATE", "RAISE"],
                    PayoffTokens: ["COMMAND", "SACRIFICE", "BONE", "LEGION", "ARMY", "NECROMANCY"],
                    SupportTokens: ["DRAW", "ENERGY", "BLOCK", "WEAK"],
                    AvoidTokens: []),
                new(
                    "necrobinder_void",
                    CoreTokens: ["VOID", "WRAITH", "GHOST", "ETHEREAL", "PHANTOM", "SPIRIT", "SOUL"],
                    PayoffTokens: ["HAUNT", "POSSESS", "APPARITION", "INTANGIBLE", "DRAIN"],
                    SupportTokens: ["DRAW", "ENERGY", "RETAIN", "WEAK"],
                    AvoidTokens: []),
                new(
                    "necrobinder_calamity",
                    CoreTokens: ["CALAMITY", "DOOM", "CURSE", "HEX", "PLAGUE", "BLIGHT", "RUIN", "DECAY"],
                    PayoffTokens: ["DISASTER", "APOCALYPSE", "MORBID", "GRAVE", "DRAIN", "SACRIFICE"],
                    SupportTokens: ["DRAW", "ENERGY", "BLOCK", "VULNERABLE", "WEAK"],
                    AvoidTokens: []),
                new(
                    "necrobinder_sacrifice",
                    CoreTokens: ["SACRIFICE", "BLOOD", "OFFERING", "PACT", "CONTRACT", "DRAIN", "LIFE"],
                    PayoffTokens: ["REANIMATE", "SOUL", "BONE", "STRENGTH", "POWER"],
                    SupportTokens: ["HEAL", "BLOCK", "DRAW", "SUMMON"],
                    AvoidTokens: [])
            ]
        };

    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        CharacterFamily family = ResolveFamily(context, card);
        if (family == CharacterFamily.Unknown ||
            !Definitions.TryGetValue(family, out IReadOnlyList<ArchetypeDefinition>? definitions))
        {
            return 0d;
        }

        IReadOnlyList<ArchetypeProfile> profiles = definitions
            .Select(definition => EvaluateDeck(definition, context.DeckCards))
            .OrderByDescending(static profile => profile.Score)
            .ToArray();
        if (profiles.Count == 0)
        {
            return 0d;
        }

        ArchetypeProfile best = profiles[0];
        double score = 0d;
        bool candidateIsArchetypeCard = false;
        foreach (ArchetypeProfile profile in profiles)
        {
            CardArchetypeRole role = EvaluateCard(profile.Definition, card);
            if (role.Total <= 0d && role.Avoid <= 0d)
            {
                continue;
            }

            candidateIsArchetypeCard = candidateIsArchetypeCard || role.Total > 0d;
            bool isBest = string.Equals(profile.Definition.Id, best.Definition.Id, StringComparison.Ordinal);
            double deckSignal = profile.Score;
            double commitment = deckSignal >= CommittedThreshold
                ? 1.0d
                : deckSignal >= EmergingThreshold ? 0.62d : 0.35d;
            double roleScore = role.Core * 4.0d + role.Payoff * 3.4d + role.Support * 1.8d - role.Avoid * 7.0d;

            if (deckSignal < EmergingThreshold && role.Payoff > role.Core && role.Support <= 0d)
            {
                roleScore -= 4.0d;
            }

            if (isBest)
            {
                score += roleScore * commitment;
                if (deckSignal >= EmergingThreshold && role.Total > 0d)
                {
                    score += Math.Min(deckSignal, 18d) * 0.18d;
                }
            }
            else
            {
                score += roleScore * Math.Min(commitment, 0.42d);
            }
        }

        if (best.Score >= CommittedThreshold &&
            !candidateIsArchetypeCard &&
            !IsFoundationCard(card, context) &&
            IsLowImpactFiller(card))
        {
            score -= Math.Min(8d, 3d + (best.Score - CommittedThreshold) * 0.35d);
        }

        if (best.Score >= EmergingThreshold && candidateIsArchetypeCard)
        {
            score += GetLateCommitmentBonus(context, best.Score);
        }

        return score;
    }

    public static string DescribeTopArchetypes(CardEvaluationContext context, int maxCount = 2)
    {
        CharacterFamily family = ResolveFamily(context, null);
        if (family == CharacterFamily.Unknown ||
            !Definitions.TryGetValue(family, out IReadOnlyList<ArchetypeDefinition>? definitions))
        {
            return "none";
        }

        return string.Join(", ", definitions
            .Select(definition => EvaluateDeck(definition, context.DeckCards))
            .OrderByDescending(static profile => profile.Score)
            .Take(maxCount)
            .Where(static profile => profile.Score > 0d)
            .Select(static profile => $"{profile.Definition.Id}:{profile.Score:F1}"));
    }

    private static double GetLateCommitmentBonus(CardEvaluationContext context, double deckSignal)
    {
        if (context.TotalFloor < 12 && context.CurrentActIndex == 0)
        {
            return 0d;
        }

        return Math.Min(4d, deckSignal * 0.12d);
    }

    private static ArchetypeProfile EvaluateDeck(ArchetypeDefinition definition, IReadOnlyList<ResolvedCardView> deckCards)
    {
        double score = 0d;
        int coreCount = 0;
        int payoffCount = 0;
        foreach (ResolvedCardView card in deckCards)
        {
            CardArchetypeRole role = EvaluateCard(definition, card);
            if (role.Core > 0d)
            {
                coreCount++;
            }

            if (role.Payoff > 0d)
            {
                payoffCount++;
            }

            score += role.Core * 2.4d + role.Payoff * 1.8d + role.Support * 0.85d - role.Avoid * 1.2d;
        }

        if (coreCount > 0 && payoffCount > 0)
        {
            score += Math.Min(coreCount, 4) * 0.8d + Math.Min(payoffCount, 3) * 1.0d;
        }

        return new ArchetypeProfile(definition, score);
    }

    private static CardArchetypeRole EvaluateCard(ArchetypeDefinition definition, ResolvedCardView card)
    {
        double core = CountMatches(card, definition.CoreTokens);
        double payoff = CountMatches(card, definition.PayoffTokens);
        double support = CountMatches(card, definition.SupportTokens);
        double avoid = CountMatches(card, definition.AvoidTokens);

        core += EffectRoleBonus(definition.Id, card, RoleKind.Core);
        payoff += EffectRoleBonus(definition.Id, card, RoleKind.Payoff);
        support += EffectRoleBonus(definition.Id, card, RoleKind.Support);
        avoid += EffectRoleBonus(definition.Id, card, RoleKind.Avoid);

        return new CardArchetypeRole(core, payoff, support, avoid);
    }

    private static double EffectRoleBonus(string archetypeId, ResolvedCardView card, RoleKind role)
    {
        return archetypeId switch
        {
            "ironclad_vulnerable" when role == RoleKind.Core && card.GetEnemyVulnerableAmount() > 0 => 1.3d,
            "ironclad_strength" when role == RoleKind.Core && card.GetSelfStrengthAmount() > card.GetSelfTemporaryStrengthAmount() => 1.2d,
            "ironclad_block_body" when role == RoleKind.Core && card.GetEstimatedBlock() >= 10 => 0.8d,
            "silent_poison" when role == RoleKind.Core && AppliesPowerLike(card, "POISON") => 1.5d,
            "silent_defense_control" when role == RoleKind.Core && (card.GetEnemyWeakAmount() > 0 || card.GetSelfDexterityAmount() > 0) => 1.0d,
            "regent_starlight" when role == RoleKind.Core && (card.GetStarsGenerated() > 0 || card.StarCost > 0 || card.HasXStarCost) => 1.4d,
            "defect_orb_charge" when role == RoleKind.Core && HasOrbEffect(card) => 1.5d,
            "defect_power" when role == RoleKind.Core && card.Type == CardType.Power => 0.9d,
            "necrobinder_summon" when role == RoleKind.Core && card.GetSummonAmount() > 0 => 1.8d,
            "necrobinder_summon" when role == RoleKind.Payoff && IsOstyAttackCard(card) => 1.2d,
            "necrobinder_summon" when role == RoleKind.Support && card.GetEstimatedProtection() > 0 => 0.7d,
            "necrobinder_void" when role == RoleKind.Core && card.Ethereal => 0.8d,
            _ => 0d
        };
    }

    private static bool IsFoundationCard(ResolvedCardView card, CardEvaluationContext context)
    {
        return card.GetEstimatedProtection() > 0 && NeedsMoreDefense(context.DeckSummary) ||
               card.GetCardsDrawn() > 0 && NeedsMoreDraw(context.DeckSummary) ||
               card.GetEnergyGain() > 0 && NeedsMoreEnergy(context.DeckSummary) ||
               card.GetEnemyWeakAmount() > 0 ||
               card.GetEnemyVulnerableAmount() > 0 ||
               card.DealsDamageToAllEnemies() && context.DeckSummary.AoESources == 0 ||
               card.GetEstimatedDamage() >= 16 && context.DeckSummary.QualityDamageSources < DesiredQualityDamageSources(context.DeckSummary);
    }

    private static bool IsLowImpactFiller(ResolvedCardView card)
    {
        int value = card.GetEstimatedDamage() +
                    card.GetEstimatedProtection() +
                    card.GetCardsDrawn() * 4 +
                    card.GetEnergyGain() * 5 +
                    card.GetEnemyVulnerableAmount() * 3 +
                    card.GetEnemyWeakAmount() * 3;
        return card.Type is CardType.Attack or CardType.Skill &&
               value <= 12 &&
               !card.DealsDamageToAllEnemies() &&
               card.GetEnemyVulnerableAmount() == 0 &&
               card.GetEnemyWeakAmount() == 0;
    }

    private static CharacterFamily ResolveFamily(CardEvaluationContext context, ResolvedCardView? candidate)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (Contains(characterId, "IRONCLAD"))
        {
            return CharacterFamily.Ironclad;
        }

        if (Contains(characterId, "SILENT"))
        {
            return CharacterFamily.Silent;
        }

        if (Contains(characterId, "DEFECT"))
        {
            return CharacterFamily.Defect;
        }

        if (Contains(characterId, "REGENT"))
        {
            return CharacterFamily.Regent;
        }

        if (Contains(characterId, "NECROBINDER") || Contains(characterId, "NECRO"))
        {
            return CharacterFamily.Necrobinder;
        }

        IEnumerable<ResolvedCardView> cards = candidate != null
            ? context.DeckCards.Append(candidate)
            : context.DeckCards;
        return GuessFamilyFromCards(cards);
    }

    private static CharacterFamily GuessFamilyFromCards(IEnumerable<ResolvedCardView> cards)
    {
        int ironclad = 0;
        int silent = 0;
        int defect = 0;
        int regent = 0;
        int necrobinder = 0;
        foreach (ResolvedCardView card in cards)
        {
            string token = BuildSearchText(card);
            if (ContainsAny(token, "IRONCLAD", "BASH", "DROPKICK", "FEEL_NO_PAIN", "BURNING_PACT", "DEMON_FORM"))
            {
                ironclad++;
            }

            if (ContainsAny(token, "SILENT", "SHIV", "POISON", "TACTICIAN", "REFLEX", "FOOTWORK"))
            {
                silent++;
            }

            if (ContainsAny(token, "DEFECT", "ORB", "FOCUS", "ZAP", "DUALCAST", "DEFRAGMENT", "CAPACITOR"))
            {
                defect++;
            }

            if (RegentCharacterStrategy.IsRegentCard(card))
            {
                regent++;
            }

            if (ContainsAny(token, "NECROBINDER", "NECRO", "SUMMON", "VOID", "CALAMITY", "SOUL"))
            {
                necrobinder++;
            }
        }

        (CharacterFamily Family, int Score)[] scores =
        [
            (CharacterFamily.Ironclad, ironclad),
            (CharacterFamily.Silent, silent),
            (CharacterFamily.Defect, defect),
            (CharacterFamily.Regent, regent),
            (CharacterFamily.Necrobinder, necrobinder)
        ];
        (CharacterFamily family, int score) = scores.OrderByDescending(static entry => entry.Score).First();
        return score > 0 ? family : CharacterFamily.Unknown;
    }

    private static double CountMatches(ResolvedCardView card, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0d;
        }

        string searchText = BuildSearchText(card);
        double matches = tokens.Count(token => Contains(searchText, token));
        if (matches == 0d)
        {
            return 0d;
        }

        return Math.Min(matches, 3d);
    }

    private static bool AppliesPowerLike(ResolvedCardView card, string powerToken)
    {
        return card.Effects.Any(effect =>
            effect.Kind == EffectKind.ApplyPower &&
            !string.IsNullOrWhiteSpace(effect.AppliedPowerId) &&
            Contains(effect.AppliedPowerId, powerToken));
    }

    private static bool HasOrbEffect(ResolvedCardView card)
    {
        return card.HasOrbSemanticEffect();
    }

    private static bool IsOstyAttackCard(ResolvedCardView card)
    {
        return ContainsAny(BuildSearchText(card), "OSTY_ATTACK", "OSTY_DAMAGE", "OSTY");
    }

    private static bool NeedsMoreDefense(DeckSummary deck)
    {
        return deck.BlockSources < DesiredBlockSources(deck) ||
               deck.QualityDefenseSources < DesiredQualityDefenseSources(deck);
    }

    private static bool NeedsMoreDraw(DeckSummary deck)
    {
        return deck.DrawSources < (deck.CardCount < 18 ? 2 : deck.CardCount < 26 ? 3 : 4);
    }

    private static bool NeedsMoreEnergy(DeckSummary deck)
    {
        return deck.EnergySources < (deck.CardCount < 18 ? 1 : 2);
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

    private static int DesiredQualityDamageSources(DeckSummary deck)
    {
        if (deck.CardCount < 12)
        {
            return 2;
        }

        return deck.CardCount < 20 ? 3 : 4;
    }

    private static string BuildSearchText(ResolvedCardView card)
    {
        IEnumerable<string> effectTokens = card.Effects.Select(effect =>
            $"{effect.Kind}_{effect.AppliedPowerId}_{effect.TargetScope}_{effect.DurationHint}_{effect.ValueTiming}");
        return Normalize(string.Join('_',
            new[]
            {
                card.CardId,
                card.Name,
                card.Type.ToString(),
                card.Rarity,
                card.Targeting.ToString()
            }
            .Concat(card.Keywords)
            .Concat(card.Tags)
            .Concat(effectTokens)));
    }

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        return tokens.Any(token => Contains(value, token));
    }

    private static bool Contains(string? value, string token)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Normalize(value).Contains(Normalize(token), StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(' ', '_')
            .Replace('-', '_')
            .Replace(':', '_')
            .Replace('/', '_')
            .ToUpperInvariant();
    }

    private enum CharacterFamily
    {
        Unknown,
        Ironclad,
        Silent,
        Defect,
        Regent,
        Necrobinder
    }

    private enum RoleKind
    {
        Core,
        Payoff,
        Support,
        Avoid
    }

    private sealed record ArchetypeDefinition(
        string Id,
        IReadOnlyList<string> CoreTokens,
        IReadOnlyList<string> PayoffTokens,
        IReadOnlyList<string> SupportTokens,
        IReadOnlyList<string> AvoidTokens);

    private readonly record struct ArchetypeProfile(ArchetypeDefinition Definition, double Score);

    private readonly record struct CardArchetypeRole(double Core, double Payoff, double Support, double Avoid)
    {
        public double Total => Core + Payoff + Support;
    }
}
