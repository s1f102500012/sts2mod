using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal static class CardRewardPriorityPlanner
{
    private const double EarlyActMultiplier = 1.18d;
    private const double LateActOneMultiplier = 1.03d;
    private const double LaterActsMultiplier = 0.86d;

    private static readonly IReadOnlyList<PriorityGroup> GenericGroups =
    [
        new(
            "generic_status_cleanup",
            DesiredTotal: 2,
            BaseScore: 31d,
            Rules:
            [
                Rule(CardPriorityRole.Cleanup, 1, 14d, ["BURNING_PACT", "TRUE_GRIT", "PREPARED", "CALCULATED_GAMBLE", "RECYCLE", "EXHAUST", "DISCARD", "PURGE", "CLEANSE"], StatusCardStrategy.IsLikelyHandCleanupCard)
            ]),
        new(
            "generic_aoe",
            DesiredTotal: 2,
            BaseScore: 22d,
            Rules:
            [
                Rule(CardPriorityRole.Aoe, 2, 8d, ["CLEAVE", "IMMOLATE", "DAGGER_SPRAY", "CORPSE_EXPLOSION", "ELECTRODYNAMICS", "SWEEPING_BEAM", "THUNDER", "AOE"], static card => card.DealsDamageToAllEnemies())
            ]),
        new(
            "generic_debuff",
            DesiredTotal: 3,
            BaseScore: 20d,
            Rules:
            [
                Rule(CardPriorityRole.Debuff, 2, 9d, ["UPPERCUT", "BASH", "SHOCKWAVE", "THUNDERCLAP", "NEUTRALIZE", "PIERCING_WAIL", "GO_FOR_THE_EYES", "ENFEEBLING_TOUCH", "WEAK", "VULNERABLE"], static card => card.GetEnemyWeakAmount() + card.GetEnemyVulnerableAmount() > 0),
                Rule(CardPriorityRole.Debuff, 2, 5d, ["POISON", "DEADLY_POISON", "BOUNCING_FLASK"], static card => card.GetEnemyPoisonAmount() > 0)
            ]),
        new(
            "generic_draw",
            DesiredTotal: 4,
            BaseScore: 18d,
            Rules:
            [
                Rule(CardPriorityRole.Draw, 4, 8d, ["DRAW", "SHRUG", "DAGGER_THROW", "ACROBATICS", "COOLHEADED", "SKIM", "COMPILE_DRIVER", "POMMEL", "BATTLE_TRANCE"], static card => card.GetCardsDrawn() > 0)
            ]),
        new(
            "generic_scaling",
            DesiredTotal: 3,
            BaseScore: 17d,
            Rules:
            [
                Rule(CardPriorityRole.Scaling, 2, 8d, ["INFLAME", "FOOTWORK", "DEFRAGMENT", "BIAS", "CAPACITOR", "ECHO_FORM", "ARSENAL", "ORBIT", "POWER"], static card => card.Type == CardType.Power || HasPersistentStatGain(card))
            ])
    ];

    private static readonly IReadOnlyDictionary<CharacterFamily, IReadOnlyList<PriorityGroup>> CharacterGroups =
        new Dictionary<CharacterFamily, IReadOnlyList<PriorityGroup>>
        {
            [CharacterFamily.Ironclad] =
            [
                new(
                    "ironclad_act1_foundation",
                    DesiredTotal: 5,
                    BaseScore: 30d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 2, 13d, CharacterRewardProfiles.Ironclad.PremiumAttackTokens),
                        Rule(CardPriorityRole.Defense, 3, 13d, CharacterRewardProfiles.Ironclad.PremiumDefenseTokens)
                    ]),
                new(
                    "ironclad_frontload",
                    DesiredTotal: 4,
                    BaseScore: 24d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 2, 9d, ["ANGER", "POMMEL_STRIKE", "PERFECTED_STRIKE", "UPPERCUT", "CARNAGE", "IMMOLATE", "RAMPAGE", "THRASH", "BODYSlam", "BODY_SLAM"], static card => card.Type == CardType.Attack && card.GetEstimatedDamage() >= 10),
                        Rule(CardPriorityRole.Debuff, 2, 8d, ["BASH", "DROPKICK", "UPPERCUT", "SHOCKWAVE", "THUNDERCLAP", "INTIMIDATE"], static card => card.GetEnemyVulnerableAmount() + card.GetEnemyWeakAmount() > 0)
                    ]),
                new(
                    "ironclad_block_exhaust",
                    DesiredTotal: 5,
                    BaseScore: 22d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Defense, 3, 8d, ["SHRUG_IT_OFF", "TRUE_GRIT", "ARMAMENT", "FLAME_BARRIER", "IMPERVIOUS", "RAGE"], static card => card.GetEstimatedProtection() >= 8 || card.GetEstimatedProtection() >= 5 && card.GetCardsDrawn() > 0),
                        Rule(CardPriorityRole.Cleanup, 2, 10d, ["BURNING_PACT", "TRUE_GRIT", "SECOND_WIND", "FIEND_FIRE", "SEVER_SOUL"], StatusCardStrategy.IsLikelyHandCleanupCard)
                    ]),
                new(
                    "ironclad_scaling_payoff",
                    DesiredTotal: 3,
                    BaseScore: 18d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Scaling, 2, 8d, ["INFLAME", "SPOT_WEAKNESS", "LIMIT_BREAK", "DEMON_FORM", "RUPTURE"], static card => HasPersistentStatGain(card)),
                        Rule(CardPriorityRole.Power, 2, 6d, ["FEEL_NO_PAIN", "DARK_EMBRACE", "CORRUPTION", "BARRICADE", "JUGGERNAUT"])
                    ])
            ],
            [CharacterFamily.Silent] =
            [
                new(
                    "silent_act1_foundation",
                    DesiredTotal: 5,
                    BaseScore: 30d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 2, 13d, CharacterRewardProfiles.Silent.PremiumAttackTokens),
                        Rule(CardPriorityRole.Defense, 3, 13d, CharacterRewardProfiles.Silent.PremiumDefenseTokens)
                    ]),
                new(
                    "silent_poison_control",
                    DesiredTotal: 4,
                    BaseScore: 23d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Debuff, 3, 10d, ["DEADLY_POISON", "BOUNCING_FLASK", "NOXIOUS_FUMES", "POISONED_STAB", "CATALYST", "CORPSE_EXPLOSION", "POISON"], static card => card.GetEnemyPoisonAmount() > 0),
                        Rule(CardPriorityRole.Defense, 2, 7d, ["PIERCING_WAIL", "MALAISE", "LEG_SWEEP", "FOOTWORK", "WRAITH_FORM"], static card => card.GetEnemyWeakAmount() > 0 || card.GetSelfDexterityAmount() > card.GetSelfTemporaryDexterityAmount())
                    ]),
                new(
                    "silent_discard_draw",
                    DesiredTotal: 5,
                    BaseScore: 22d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Draw, 4, 9d, ["DAGGER_THROW", "ACROBATICS", "BACKFLIP", "PREPARED", "CALCULATED_GAMBLE", "REFLEX"], static card => card.GetCardsDrawn() > 0),
                        Rule(CardPriorityRole.Energy, 2, 6d, ["TACTICIAN", "CONCENTRATE", "SNEAKY_STRIKE"], static card => card.GetEnergyGain() > 0)
                    ]),
                new(
                    "silent_shiv_when_supported",
                    DesiredTotal: 4,
                    BaseScore: 15d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 3, 6d, ["BLADE_DANCE", "CLOAK_AND_DAGGER", "SHIV", "STORM_OF_STEEL"]),
                        Rule(CardPriorityRole.Scaling, 2, 8d, ["ACCURACY", "AFTER_IMAGE", "THOUSAND_CUTS", "FINISHER"])
                    ])
            ],
            [CharacterFamily.Defect] =
            [
                new(
                    "defect_bottled_core",
                    DesiredTotal: 8,
                    BaseScore: 32d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Power, 2, 14d, ["ELECTRODYNAMICS", "ECHO_FORM", "BUFFER", "CREATIVE_AI"]),
                        Rule(CardPriorityRole.Scaling, 4, 13d, ["DEFRAGMENT", "DE_FRAGMENT", "BIASED_COGNITION", "BIAS_COGNITION", "FOCUS"]),
                        Rule(CardPriorityRole.Orb, 2, 10d, ["CAPACITOR", "LOOP", "FISSION"]),
                        Rule(CardPriorityRole.Draw, 2, 8d, ["SKIM", "COOLHEADED", "COMPILE_DRIVER"], static card => card.GetCardsDrawn() > 0)
                    ]),
                new(
                    "defect_orb_attack",
                    DesiredTotal: 4,
                    BaseScore: 25d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 2, 10d, ["BALL_LIGHTNING", "COLD_SNAP", "DOOM_AND_GLOOM", "COMPILE_DRIVER", "THUNDER", "SCRAPE"], static card => card.HasOrbSemanticEffect() && card.GetEstimatedDamage() > 0),
                        Rule(CardPriorityRole.Aoe, 1, 12d, ["ELECTRODYNAMICS", "SWEEPING_BEAM", "THUNDER"], static card => card.DealsDamageToAllEnemies())
                    ]),
                new(
                    "defect_frost_defense",
                    DesiredTotal: 6,
                    BaseScore: 23d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Defense, 4, 10d, ["COLD_SNAP", "COOLHEADED", "GLACIER", "CHILL", "CHARGE_BATTERY", "BOOT_SEQUENCE", "GENETIC_ALGORITHM"], static card => IsFrostOrDefense(card)),
                        Rule(CardPriorityRole.Defense, 2, 7d, CharacterRewardProfiles.Defect.PremiumDefenseTokens)
                    ]),
                new(
                    "defect_engine_cleanup",
                    DesiredTotal: 5,
                    BaseScore: 18d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Cleanup, 2, 11d, ["RECYCLE", "REBOOT", "SCRAPE", "HOLOGRAM", "SEEK"], StatusCardStrategy.IsLikelyHandCleanupCard),
                        Rule(CardPriorityRole.Energy, 2, 5d, ["TURBO", "DOUBLE_ENERGY", "ENERGY_SURGE", "AGGREGATE"], static card => card.GetEnergyGain() > 0)
                    ])
            ],
            [CharacterFamily.Regent] =
            [
                new(
                    "regent_act1_foundation",
                    DesiredTotal: 5,
                    BaseScore: 30d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 2, 13d, CharacterRewardProfiles.Regent.PremiumAttackTokens),
                        Rule(CardPriorityRole.Defense, 3, 13d, CharacterRewardProfiles.Regent.PremiumDefenseTokens)
                    ]),
                new(
                    "regent_starlight",
                    DesiredTotal: 5,
                    BaseScore: 22d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Scaling, 3, 9d, ["STAR", "STARS", "VENERATE", "CHILD_OF_THE_STARS", "CLOAK_OF_STARS", "PARTICLE_WALL"], static card => card.GetStarsGenerated() > 0 || card.StarCost > 0 || card.HasXStarCost),
                        Rule(CardPriorityRole.Damage, 2, 7d, ["SEVEN_STARS", "STARDUST", "DYING_STAR", "STAR_EXTINCTION", "GAMMA_BLAST"])
                    ]),
                new(
                    "regent_colorless_toolbox",
                    DesiredTotal: 3,
                    BaseScore: 15d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Power, 1, 8d, ["ARSENAL", "ORBIT", "FURNACE", "PRISM"]),
                        Rule(CardPriorityRole.Draw, 2, 6d, ["DISCOVERY", "SECRET", "COLORLESS", "APOTHEOSIS"], static card => card.GetCardsDrawn() > 0 || card.GetRecognizedUtilityAmount() >= 8)
                    ]),
                new(
                    "regent_safety",
                    DesiredTotal: 4,
                    BaseScore: 20d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Defense, 3, 9d, ["COSMIC_INDIFFERENCE", "REFLECT", "MAKE_IT_SO", "I_AM_INVINCIBLE", "GUARDS"], static card => card.GetEstimatedProtection() >= 8 || card.GetEnemyWeakAmount() > 0),
                        Rule(CardPriorityRole.Cleanup, 1, 8d, ["ROYAL_GAMBLE", "GAMBLE", "EXHAUST", "DISCARD"], StatusCardStrategy.IsLikelyHandCleanupCard)
                    ])
            ],
            [CharacterFamily.Necrobinder] =
            [
                new(
                    "necrobinder_act1_foundation",
                    DesiredTotal: 5,
                    BaseScore: 30d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 2, 13d, CharacterRewardProfiles.Necrobinder.PremiumAttackTokens),
                        Rule(CardPriorityRole.Defense, 3, 13d, CharacterRewardProfiles.Necrobinder.PremiumDefenseTokens)
                    ]),
                new(
                    "necrobinder_summon",
                    DesiredTotal: 5,
                    BaseScore: 25d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Summon, 3, 12d, ["SUMMON", "REANIMATE", "RAISE", "SKELETON", "SERVANT", "MINION", "OSTY"], static card => card.GetSummonAmount() > 0),
                        Rule(CardPriorityRole.Power, 2, 7d, ["COMMAND", "NECROMANCY", "LEGION", "ARMY", "BONE"])
                    ]),
                new(
                    "necrobinder_void_calamity",
                    DesiredTotal: 5,
                    BaseScore: 21d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Damage, 3, 8d, ["VOID", "SOUL", "WRAITH", "GHOST", "CALAMITY", "DOOM", "HEX", "FEAR", "NEGATIVE_PULSE"]),
                        Rule(CardPriorityRole.Debuff, 2, 8d, ["WEAKEN", "ENFEEBLING_TOUCH", "FEAR", "VULNERABLE"], static card => card.GetEnemyWeakAmount() + card.GetEnemyVulnerableAmount() > 0)
                    ]),
                new(
                    "necrobinder_sustain_cleanup",
                    DesiredTotal: 4,
                    BaseScore: 20d,
                    Rules:
                    [
                        Rule(CardPriorityRole.Defense, 3, 9d, ["DEFY", "GRAVE_WARDEN", "DEATHS_DOOR", "DELAY", "UNDEATH"], static card => card.GetEstimatedProtection() >= 8 || card.GetSummonAmount() > 0),
                        Rule(CardPriorityRole.Cleanup, 2, 8d, ["FETCH", "RECLAIM", "SACRIFICE", "PACT", "DISCARD", "EXHAUST"], StatusCardStrategy.IsLikelyHandCleanupCard)
                    ])
            ]
        };

    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        CharacterFamily family = ResolveFamily(context, card);
        IReadOnlyList<PriorityGroup> characterGroups = CharacterGroups.TryGetValue(family, out IReadOnlyList<PriorityGroup>? groups)
            ? groups
            : [];

        List<PriorityGroup> allGroups = [.. GenericGroups, .. characterGroups];
        double bestWantedScore = 0d;
        double secondarySupportScore = 0d;
        double overfillPenalty = 0d;
        int groupIndex = 0;

        foreach (PriorityGroup group in allGroups)
        {
            int groupCopies = CountGroupCopies(context.DeckCards, group);
            bool groupFull = group.DesiredTotal > 0 && groupCopies >= GetAdjustedGroupTarget(group, context);
            int ruleIndex = 0;

            foreach (PriorityRule rule in group.Rules)
            {
                if (!MatchesRule(card, rule))
                {
                    ruleIndex++;
                    continue;
                }

                int ruleCopies = CountRuleCopies(context.DeckCards, rule);
                int desiredCopies = GetAdjustedRuleTarget(rule, context);
                bool ruleFull = desiredCopies > 0 && ruleCopies >= desiredCopies;
                if (groupFull || ruleFull)
                {
                    overfillPenalty += GetOverfillPenalty(rule, card, context, groupFull, ruleFull);
                    ruleIndex++;
                    continue;
                }

                double ruleScore = group.BaseScore + rule.Bonus + GetPriorityOrderBonus(groupIndex, ruleIndex);
                ruleScore *= GetRoleNeedMultiplier(rule.Role, card, context);
                ruleScore *= GetPhaseMultiplier(context);
                ruleScore += GetMissingPackageBonus(rule.Role, context);
                ruleScore += GetRelicAdjustment(rule.Role, card, context);

                if (ruleScore > bestWantedScore)
                {
                    secondarySupportScore += bestWantedScore * 0.14d;
                    bestWantedScore = ruleScore;
                }
                else
                {
                    secondarySupportScore += ruleScore * 0.14d;
                }

                ruleIndex++;
            }

            groupIndex++;
        }

        double score = bestWantedScore + Math.Min(22d, secondarySupportScore) - overfillPenalty;
        score += ScoreCharacterAvoidance(card, context, family);
        score += ScoreMultiplayerFit(card, context);
        score += ScoreUnknownFillerPenalty(card, context, bestWantedScore);

        return Math.Clamp(score, -55d, 105d);
    }

    private static double GetRoleNeedMultiplier(CardPriorityRole role, ResolvedCardView card, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        bool defenseBehind = NeedsMoreDefense(deck);
        bool drawBehind = NeedsMoreDraw(deck);
        bool damageBehind = NeedsMoreDamage(deck);
        bool severeDamageDeficit = HasSevereDamageDeficit(deck);

        return role switch
        {
            CardPriorityRole.Damage when severeDamageDeficit => 1.55d,
            CardPriorityRole.Damage when damageBehind && !defenseBehind => 1.25d,
            CardPriorityRole.Damage when IsAttackAhead(deck) && (defenseBehind || drawBehind) => 0.48d,
            CardPriorityRole.Aoe when deck.AoESources == 0 && context.CurrentActIndex <= 1 => 1.45d,
            CardPriorityRole.Aoe when deck.AoESources >= 2 => 0.58d,
            CardPriorityRole.Defense when defenseBehind => 1.42d,
            CardPriorityRole.Defense when severeDamageDeficit && card.GetCardsDrawn() == 0 && card.GetEnemyWeakAmount() == 0 => 0.58d,
            CardPriorityRole.Draw when drawBehind => deck.EnergySources > deck.DrawSources ? 1.55d : 1.32d,
            CardPriorityRole.Draw when deck.DrawSources >= DesiredDrawSources(deck) + 1 => 0.62d,
            CardPriorityRole.Energy when deck.DrawSources >= 3 && NeedsMoreEnergy(deck) => 1.22d,
            CardPriorityRole.Energy when drawBehind || deck.EnergySources >= DesiredEnergySources(deck) => 0.45d,
            CardPriorityRole.Debuff when deck.VulnerableSources + deck.WeakSources <= 1 => 1.22d,
            CardPriorityRole.Cleanup when deck.StatusHandlingCards == 0 => 1.7d,
            CardPriorityRole.Cleanup when deck.StatusHandlingCards >= 2 && deck.BadCards == 0 => 0.45d,
            CardPriorityRole.Scaling when context.CurrentActIndex == 0 && context.TotalFloor <= 5 && !HasImmediateCombatValue(card) => 0.68d,
            CardPriorityRole.Scaling when deck.ScalingSources <= 1 && (context.ActFloor >= 8 || context.CurrentActIndex >= 1) => 1.25d,
            CardPriorityRole.Power when context.CurrentActIndex == 0 && context.TotalFloor <= 5 && !HasImmediateCombatValue(card) => 0.72d,
            CardPriorityRole.Orb when deck.OrbCards >= 2 || card.HasOrbSemanticEffect() => 1.18d,
            CardPriorityRole.Summon when deck.BlockSources < DesiredBlockSources(deck) => 1.22d,
            _ => 1d
        };
    }

    private static double GetMissingPackageBonus(CardPriorityRole role, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        return role switch
        {
            CardPriorityRole.Damage when HasSevereDamageDeficit(deck) => 10d,
            CardPriorityRole.Defense when NeedsMoreDefense(deck) => 8d,
            CardPriorityRole.Draw when NeedsMoreDraw(deck) => 7d,
            CardPriorityRole.Energy when NeedsMoreEnergy(deck) && deck.DrawSources >= 2 => 4d,
            CardPriorityRole.Cleanup when deck.StatusHandlingCards == 0 => context.CurrentActIndex == 0 ? 18d : 12d,
            CardPriorityRole.Aoe when deck.AoESources == 0 && context.TotalFloor <= 18 => 7d,
            CardPriorityRole.Scaling when deck.ScalingSources == 0 && context.TotalFloor >= 8 => 6d,
            _ => 0d
        };
    }

    private static double GetRelicAdjustment(CardPriorityRole role, ResolvedCardView card, CardEvaluationContext context)
    {
        double score = 0d;
        bool hasSneckoEye = HasRelic(context, "SNECKO");
        bool hasRunicPyramid = HasRelic(context, "RUNIC_PYRAMID", "PYRAMID");

        if (hasSneckoEye)
        {
            if (card.EffectiveCost >= 2 && (card.GetEstimatedDamage() + card.GetEstimatedProtection() >= 16 || card.Type == CardType.Power))
            {
                score += 6d;
            }

            if (card.EffectiveCost == 0 && role is CardPriorityRole.Damage or CardPriorityRole.Defense && card.GetCardsDrawn() == 0)
            {
                score -= 7d;
            }
        }

        if (hasRunicPyramid)
        {
            if (role == CardPriorityRole.Draw && card.GetEnergyGain() == 0)
            {
                score -= 4d;
            }

            if (role == CardPriorityRole.Cleanup || card.GetEnergyGain() > 0)
            {
                score += 5d;
            }

            if (card.Retain && card.GetEstimatedDamage() + card.GetEstimatedProtection() + card.GetRecognizedUtilityAmount() <= 10)
            {
                score -= 5d;
            }
        }

        return score;
    }

    private static double ScoreCharacterAvoidance(ResolvedCardView card, CardEvaluationContext context, CharacterFamily family)
    {
        if (family == CharacterFamily.Unknown)
        {
            return 0d;
        }

        string[] weakTokens = family switch
        {
            CharacterFamily.Ironclad => [.. CharacterRewardProfiles.Ironclad.WeakAttackTokens, .. CharacterRewardProfiles.Ironclad.WeakDefenseTokens],
            CharacterFamily.Silent => [.. CharacterRewardProfiles.Silent.WeakAttackTokens, .. CharacterRewardProfiles.Silent.WeakDefenseTokens],
            CharacterFamily.Defect => [.. CharacterRewardProfiles.Defect.WeakAttackTokens, .. CharacterRewardProfiles.Defect.WeakDefenseTokens],
            CharacterFamily.Regent => [.. CharacterRewardProfiles.Regent.WeakAttackTokens, .. CharacterRewardProfiles.Regent.WeakDefenseTokens],
            CharacterFamily.Necrobinder => [.. CharacterRewardProfiles.Necrobinder.WeakAttackTokens, .. CharacterRewardProfiles.Necrobinder.WeakDefenseTokens],
            _ => []
        };
        if (!MatchesTokens(card, weakTokens))
        {
            return 0d;
        }

        double penalty = context.CurrentActIndex == 0
            ? context.TotalFloor <= 8 ? 25d : 17d
            : 9d;

        if (StatusCardStrategy.IsLikelyHandCleanupCard(card) && context.DeckSummary.StatusHandlingCards == 0)
        {
            penalty *= 0.35d;
        }

        if (card.Type == CardType.Power && context.ActFloor >= 10 && context.DeckSummary.ScalingSources <= 1)
        {
            penalty *= 0.55d;
        }

        return -penalty;
    }

    private static double ScoreMultiplayerFit(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!card.IsMultiplayerOnlyCard() || context.Player.RunState.Players.Count <= 1)
        {
            return 0d;
        }

        double score = 9d;
        if (context.ChoiceSource == CardChoiceSource.Shop)
        {
            score += 3d;
        }

        if (card.GetCardsDrawn() > 0 || card.GetEstimatedProtection() > 0 || card.GetEnemyWeakAmount() > 0 || card.GetEnemyVulnerableAmount() > 0)
        {
            score += 4d;
        }

        return score;
    }

    private static double ScoreUnknownFillerPenalty(ResolvedCardView card, CardEvaluationContext context, double bestWantedScore)
    {
        if (bestWantedScore > 0d ||
            context.DeckSummary.CardCount < 14 ||
            card.Type is not (CardType.Attack or CardType.Skill))
        {
            return 0d;
        }

        int knownValue = card.GetEstimatedDamage() +
                         card.GetEstimatedProtection() +
                         card.GetCardsDrawn() * 4 +
                         card.GetEnergyGain() * 5 +
                         card.GetEnemyWeakAmount() * 3 +
                         card.GetEnemyVulnerableAmount() * 3 +
                         card.GetEnemyPoisonAmount() * 2 +
                         card.GetRecognizedUtilityAmount();
        if (knownValue > 12 || card.DealsDamageToAllEnemies() || card.IsMultiplayerOnlyCard())
        {
            return 0d;
        }

        return context.SkipAllowed ? -8d : -3d;
    }

    private static double GetOverfillPenalty(
        PriorityRule rule,
        ResolvedCardView card,
        CardEvaluationContext context,
        bool groupFull,
        bool ruleFull)
    {
        double penalty = rule.Role switch
        {
            CardPriorityRole.Damage when HasSevereDamageDeficit(context.DeckSummary) => 0d,
            CardPriorityRole.Defense when NeedsMoreDefense(context.DeckSummary) => 0d,
            CardPriorityRole.Draw when NeedsMoreDraw(context.DeckSummary) => 0d,
            CardPriorityRole.Cleanup when context.DeckSummary.StatusHandlingCards == 0 => 0d,
            CardPriorityRole.Aoe when context.DeckSummary.AoESources == 0 => 0d,
            CardPriorityRole.Power or CardPriorityRole.Scaling when card.Rarity == "Rare" && context.DeckSummary.ScalingSources <= 1 => 2d,
            _ => ruleFull ? 8d : 5d
        };

        if (groupFull && ruleFull)
        {
            penalty += 4d;
        }

        if (context.ChoiceSource == CardChoiceSource.Shop)
        {
            penalty += 3d;
        }

        return penalty;
    }

    private static int GetAdjustedGroupTarget(PriorityGroup group, CardEvaluationContext context)
    {
        if (group.Id.Contains("status_cleanup", StringComparison.Ordinal) && context.DeckSummary.StatusHandlingCards == 0)
        {
            return Math.Max(1, group.DesiredTotal);
        }

        if (context.CurrentActIndex >= 1 && group.Id.Contains("foundation", StringComparison.Ordinal))
        {
            return Math.Max(3, group.DesiredTotal - 1);
        }

        return group.DesiredTotal;
    }

    private static int GetAdjustedRuleTarget(PriorityRule rule, CardEvaluationContext context)
    {
        if (rule.Role == CardPriorityRole.Cleanup)
        {
            return context.DeckSummary.StatusHandlingCards == 0 ? Math.Max(1, rule.DesiredCopies) : rule.DesiredCopies;
        }

        if (rule.Role == CardPriorityRole.Draw && context.DeckSummary.EnergySources > context.DeckSummary.DrawSources)
        {
            return rule.DesiredCopies + 1;
        }

        if (rule.Role == CardPriorityRole.Energy && context.DeckSummary.DrawSources <= 1)
        {
            return Math.Max(1, rule.DesiredCopies - 1);
        }

        return rule.DesiredCopies;
    }

    private static double GetPriorityOrderBonus(int groupIndex, int ruleIndex)
    {
        return Math.Max(0d, 13d - groupIndex * 1.15d) + Math.Max(0d, 5d - ruleIndex * 0.55d);
    }

    private static double GetPhaseMultiplier(CardEvaluationContext context)
    {
        if (context.CurrentActIndex == 0 && context.TotalFloor <= 8)
        {
            return EarlyActMultiplier;
        }

        if (context.CurrentActIndex == 0)
        {
            return LateActOneMultiplier;
        }

        return LaterActsMultiplier;
    }

    private static bool MatchesRule(ResolvedCardView card, PriorityRule rule)
    {
        return MatchesTokens(card, rule.Tokens) ||
               rule.EffectPredicate?.Invoke(card) == true;
    }

    private static int CountRuleCopies(IEnumerable<ResolvedCardView> cards, PriorityRule rule)
    {
        return cards.Count(card => MatchesRule(card, rule));
    }

    private static int CountGroupCopies(IEnumerable<ResolvedCardView> cards, PriorityGroup group)
    {
        return cards.Count(card => group.Rules.Any(rule => MatchesRule(card, rule)));
    }

    private static PriorityRule Rule(
        CardPriorityRole role,
        int desiredCopies,
        double bonus,
        IReadOnlyList<string> tokens,
        Func<ResolvedCardView, bool>? effectPredicate = null)
    {
        return new PriorityRule(role, desiredCopies, bonus, tokens, effectPredicate);
    }

    private static bool HasImmediateCombatValue(ResolvedCardView card)
    {
        return card.GetEstimatedDamage() > 0 ||
               card.GetEstimatedProtection() > 0 ||
               card.GetCardsDrawn() > 0 ||
               card.GetEnergyGain() > 0 ||
               card.GetEnemyWeakAmount() > 0 ||
               card.GetEnemyVulnerableAmount() > 0;
    }

    private static bool HasPersistentStatGain(ResolvedCardView card)
    {
        return card.GetSelfStrengthAmount() > card.GetSelfTemporaryStrengthAmount() ||
               card.GetSelfDexterityAmount() > card.GetSelfTemporaryDexterityAmount();
    }

    private static bool IsFrostOrDefense(ResolvedCardView card)
    {
        return card.GetEstimatedProtection() > 0 ||
               card.HasOrbSemanticEffect() && MatchesTokens(card, ["FROST", "COLD_SNAP", "COOLHEADED", "GLACIER", "CHILL", "ICE"]);
    }

    private static bool NeedsMoreDamage(DeckSummary deck)
    {
        return deck.FrontloadDamageSources < DesiredDamageSources(deck) ||
               deck.QualityDamageSources < DesiredQualityDamageSources(deck);
    }

    private static bool HasSevereDamageDeficit(DeckSummary deck)
    {
        return deck.FrontloadDamageSources <= Math.Max(2, DesiredDamageSources(deck) - 3) ||
               deck.QualityDamageSources == 0 && deck.FrontloadDamageSources < DesiredDamageSources(deck);
    }

    private static bool NeedsMoreDefense(DeckSummary deck)
    {
        return deck.BlockSources < DesiredBlockSources(deck) ||
               deck.QualityDefenseSources < DesiredQualityDefenseSources(deck);
    }

    private static bool NeedsMoreDraw(DeckSummary deck)
    {
        return deck.DrawSources < DesiredDrawSources(deck);
    }

    private static bool NeedsMoreEnergy(DeckSummary deck)
    {
        return deck.EnergySources < DesiredEnergySources(deck);
    }

    private static bool IsAttackAhead(DeckSummary deck)
    {
        return deck.FrontloadDamageSources >= DesiredDamageSources(deck) + 1 ||
               deck.QualityDamageSources >= DesiredQualityDamageSources(deck) + 1 ||
               deck.AttackCount >= deck.SkillCount + 3;
    }

    private static int DesiredDamageSources(DeckSummary deck)
    {
        if (deck.CardCount < 12)
        {
            return 5;
        }

        return deck.CardCount < 20 ? 6 : 7;
    }

    private static int DesiredQualityDamageSources(DeckSummary deck)
    {
        if (deck.CardCount < 12)
        {
            return 2;
        }

        return deck.CardCount < 20 ? 3 : 4;
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

    private static int DesiredDrawSources(DeckSummary deck)
    {
        if (deck.CardCount < 18)
        {
            return 2;
        }

        return deck.CardCount < 26 ? 3 : 4;
    }

    private static int DesiredEnergySources(DeckSummary deck)
    {
        return deck.CardCount < 18 ? 1 : 2;
    }

    private static bool HasRelic(CardEvaluationContext context, params string[] tokens)
    {
        return context.RelicIds.Any(relicId => tokens.Any(token => Normalize(relicId).Contains(Normalize(token), StringComparison.Ordinal)));
    }

    private static CharacterFamily ResolveFamily(CardEvaluationContext context, ResolvedCardView? candidate)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (characterId.Contains("IRONCLAD", StringComparison.Ordinal))
        {
            return CharacterFamily.Ironclad;
        }

        if (characterId.Contains("SILENT", StringComparison.Ordinal))
        {
            return CharacterFamily.Silent;
        }

        if (characterId.Contains("DEFECT", StringComparison.Ordinal))
        {
            return CharacterFamily.Defect;
        }

        if (characterId.Contains("REGENT", StringComparison.Ordinal))
        {
            return CharacterFamily.Regent;
        }

        if (characterId.Contains("NECROBINDER", StringComparison.Ordinal) || characterId.Contains("NECRO", StringComparison.Ordinal))
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
            if (ContainsAny(token, "IRONCLAD", "BASH", "DROPKICK", "BURNING_PACT", "DEMON_FORM"))
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

    private static bool MatchesTokens(ResolvedCardView card, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return false;
        }

        string searchText = BuildSearchText(card);
        return tokens.Any(token => searchText.Contains(Normalize(token), StringComparison.Ordinal));
    }

    private static string BuildSearchText(ResolvedCardView card)
    {
        IEnumerable<string> effectTokens = card.Effects.Select(static effect =>
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
        return tokens.Any(token => value?.Contains(Normalize(token), StringComparison.Ordinal) == true);
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

    private enum CardPriorityRole
    {
        Damage,
        Defense,
        Draw,
        Energy,
        Debuff,
        Aoe,
        Scaling,
        Power,
        Orb,
        Summon,
        Cleanup
    }

    private sealed record PriorityGroup(
        string Id,
        int DesiredTotal,
        double BaseScore,
        IReadOnlyList<PriorityRule> Rules);

    private sealed record PriorityRule(
        CardPriorityRole Role,
        int DesiredCopies,
        double Bonus,
        IReadOnlyList<string> Tokens,
        Func<ResolvedCardView, bool>? EffectPredicate);
}
