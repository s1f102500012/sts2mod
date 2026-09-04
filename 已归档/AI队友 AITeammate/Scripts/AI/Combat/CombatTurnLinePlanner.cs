using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal sealed class CombatTurnLinePlanner
{
    private const int MaxLineLength = 8;
    private const int SearchNodeBudget = 16_000;
    private const int SearchBranchWidth = 12;
    private const int ImmediateActionBiasDivisor = 4;
    private const int AllEnemiesLethalLineBonus = 10000;
    private const int NonMinionLethalLineBonus = 13500;
    private const int NonMinionLethalMinionDamagePenaltyPerPoint = 28;
    private const int NonMinionLethalMinionKillPenalty = 900;
    private const int LethalTurnWithoutKillPenalty = 2500;
    private const int TeamCrisisDamageTakenPenaltyCap = 700;
    private const int EnemyDebuffOpeningLineBonus = 80;
    private const int EnemyDebuffEarlyLineBonus = 38;
    private const int EnemyDebuffAfterDamagePenalty = 45;
    private const int GardenersPrimaryDamageLineValue = 18;
    private const int GardenersPrimaryKillLineBonus = 950;
    private const int GardenersNoPrimaryProgressPenalty = 180;
    private const int GardenersOffTargetLinePenalty = 170;
    private const int ObscuraPrimaryDamageLineValue = 30;
    private const int ObscuraPrimaryKillLineBonus = 1500;
    private const int ObscuraNoPrimaryProgressPenalty = 620;
    private const int ObscuraOffTargetLinePenalty = 720;
    private const int CatastrophicNoProgressPenalty = 650;
    private const int CatastrophicThreatKillBonus = 1800;
    private const int KaiserCrabFinalFacingTargetBonus = 900;
    private const int KaiserCrabFinalFacingWrongTargetPenalty = 620;
    private const int LagavulinSleepWakeLinePenalty = 18000;
    private const int LagavulinSleepSetupLineBonus = 380;
    private const int LagavulinSleepBlockChipLinePenaltyPerPoint = 45;
    private const int ThornsReturnedDamageTerminalPenaltyPerPoint = 42;
    private const int ThornsNonLethalLinePenalty = 180;

    private readonly CombatActionScorer _scorer;

    public CombatTurnLinePlanner(CombatActionScorer scorer)
    {
        _scorer = scorer;
    }

    public CombatLinePlan? BuildBestPlan(DeterministicCombatContext context)
    {
        return BuildCandidatePlans(context, maxPlans: 1).FirstOrDefault();
    }

    public IReadOnlyList<CombatLinePlan> BuildCandidatePlans(DeterministicCombatContext context, int maxPlans)
    {
        List<PlannableAction> executableActions = context.LegalActions
            .Select(action => BuildPlannableAction(context, action))
            .Where(static action => !action.IsEndTurn)
            .ToList();
        List<PlannableAction> actions = executableActions
            .Concat(BuildKnownDrawPlannableActions(context))
            .ToList();
        if (actions.Count == 0)
        {
            return [];
        }

        List<LineNode> bestNodes = EnumerateShadowSearchNodes(context, actions);
        if (bestNodes.Count == 0)
        {
            return [];
        }

        List<LineNode> orderedNodes = bestNodes
            .OrderByDescending(node => node.ComputeTerminalScore(context, actions))
            .ThenBy(node => string.Join("|", node.ActionIds), StringComparer.Ordinal)
            .ToList();
        LineNode best = orderedNodes.First();
        LineNode? nonMinionLethal = bestNodes
            .Where(node => node.KillsAllNonMinionEnemies(context))
            .OrderByDescending(node => node.ComputeTerminalScore(context, actions))
            .ThenBy(node => string.Join("|", node.ActionIds), StringComparer.Ordinal)
            .FirstOrDefault();
        if (nonMinionLethal != null && !best.KillsAllEnemies(context))
        {
            best = nonMinionLethal;
        }

        List<CombatLinePlan> plans = [];
        AddPlanIfWorthKeeping(ToPlan(best, context, actions));
        foreach (LineNode node in orderedNodes)
        {
            if (plans.Count >= Math.Max(1, maxPlans))
            {
                break;
            }

            if (plans.Any(plan => plan.ActionIds.SequenceEqual(node.ActionIds)))
            {
                continue;
            }

            AddPlanIfWorthKeeping(ToPlan(node, context, actions));
        }

        return plans;

        void AddPlanIfWorthKeeping(CombatLinePlan plan)
        {
            if (!IsWastefulPotionLedPlan(context, plan, actions))
            {
                plans.Add(plan);
            }
        }
    }

    private static CombatLinePlan ToPlan(
        LineNode node,
        DeterministicCombatContext context,
        IReadOnlyList<PlannableAction> actions)
    {
        return new CombatLinePlan
        {
            ActionIds = node.ActionIds.ToList(),
            Score = node.ComputeTerminalScore(context, actions),
            EstimatedDamageDealt = node.TotalDamageDealt,
            EstimatedDamageTaken = node.EstimatedDamageTaken(context),
            EstimatedBlockAfterEnemyTurn = node.EstimatedBlockAfterEnemyTurn(context)
        };
    }

    private static bool IsWastefulPotionLedPlan(
        DeterministicCombatContext context,
        CombatLinePlan plan,
        IReadOnlyList<PlannableAction> actions)
    {
        string? firstActionId = plan.FirstActionId;
        if (string.IsNullOrEmpty(firstActionId))
        {
            return false;
        }

        PlannableAction? firstAction = actions.FirstOrDefault(action =>
            string.Equals(action.Action.ActionId, firstActionId, StringComparison.Ordinal));
        if (firstAction == null ||
            !string.Equals(firstAction.Action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
            firstAction.ImmediateScore.TotalScore > 0)
        {
            return false;
        }

        if (context.IsTeamInCrisis ||
            context.ShouldSpendPotionSlotForFutureDrop ||
            plan.EstimatedDamageDealt >= Math.Max(1, context.TotalEnemyHp) ||
            plan.EstimatedDamageTaken < Math.Max(0, context.IncomingDamageAfterBlock - 8))
        {
            return false;
        }

        return true;
    }

    private static List<LineNode> EnumerateShadowSearchNodes(
        DeterministicCombatContext context,
        IReadOnlyList<PlannableAction> actions)
    {
        List<LineNode> nodes = [];
        LineNode root = new(context.Energy, context.Stars);
        int visitedNodes = 0;

        Expand(root, depth: 0);
        return nodes;

        void Expand(LineNode node, int depth)
        {
            if (visitedNodes >= SearchNodeBudget ||
                depth >= MaxLineLength ||
                node.StopExpanding)
            {
                return;
            }

            List<PlannableAction> candidates = actions
                .Where(action => ShouldConsiderLineAction(action) &&
                                 node.CanApply(context, action, actions))
                .OrderByDescending(action => EstimateSearchBranchPriority(context, node, action))
                .ThenBy(static action => action.ConsumptionKey, StringComparer.Ordinal)
                .ThenBy(static action => action.Action.ActionId, StringComparer.Ordinal)
                .Take(SearchBranchWidth)
                .ToList();
            foreach (PlannableAction candidate in candidates)
            {
                if (visitedNodes >= SearchNodeBudget)
                {
                    return;
                }

                LineNode next = node.Apply(context, candidate);
                visitedNodes++;
                nodes.Add(next);
                Expand(next, depth + 1);
            }
        }
    }

    private static int EstimateSearchBranchPriority(
        DeterministicCombatContext context,
        LineNode node,
        PlannableAction action)
    {
        int priority = action.ImmediateScore.TotalScore / Math.Max(1, ImmediateActionBiasDivisor);
        priority += action.Damage * 16;
        priority += action.Block * (context.TeamIncomingDamageAfterBlock > 0 ? 11 : 2);
        priority += action.SummonProtection * (context.TeamIncomingDamageAfterBlock > 0 ? 13 : 3);
        priority += action.IsEnemyDebuff ? 260 : 0;
        priority += action.IsSetup ? 120 : 0;
        priority += action.CardsDrawn > action.KnownBadDraws ? 90 : 0;
        priority += action.EnergyGain > 0 || action.StarsGenerated > 0 ? 70 : 0;
        if (context.TeamTactics.CanKillAllNonMinionEnemies && CanDamageNonMinionEnemy(context, action))
        {
            priority += 800;
        }

        if (node.HasPreparedVulnerableTarget(context, action) && action.Damage > 0)
        {
            priority += 180;
        }

        if (action.IsResourcePotion && !context.IsEliteOrBossCombat && !context.IsTeamInCrisis)
        {
            priority -= 180;
        }

        return priority;
    }

    private PlannableAction BuildPlannableAction(
        DeterministicCombatContext context,
        AiLegalActionOption action,
        ResolvedCardView? forcedCard = null,
        int requiredKnownDrawCount = 0,
        bool isExecutableNow = true)
    {
        CombatActionScore immediateScore = _scorer.Score(context, action);
        ResolvedCardView? card = forcedCard ?? ResolveCard(context, action);
        bool isPotion = string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal);
        OrbEvokeEstimate evoke = card.EstimateOrbEvoke(context.Actor, context.Energy, action.EnergyCost ?? 0);
        int directDamage = card.GetEstimatedDamage();
        int damage = directDamage + evoke.Damage;
        int directPotionDamage = EstimateDirectPotionDamage(action);
        if (directPotionDamage > 0)
        {
            damage = directPotionDamage;
        }

        int block = card.GetEstimatedBlock() + evoke.Block;
        int summonProtection = card.GetSummonAmount();
        if (isPotion && IsImmediateBlockPotion(action) && IsActorTarget(context, action))
        {
            block = Math.Max(block, EstimateImmediateBlockPotionAmount(action));
        }

        int cardsDrawn = card.GetCardsDrawn();
        int knownBadDraws = StatusCardStrategy.EstimateKnownBadDraws(context, cardsDrawn);
        int energyGain = Math.Max(card.GetEnergyGain() + evoke.Energy, 0);
        int starCost = Math.Max(0, card?.StarCost ?? 0);
        int starsGenerated = Math.Max(card.GetStarsGenerated(), 0);
        int vulnerable = card.GetEnemyVulnerableAmount();
        int weak = card.GetEnemyWeakAmount();
        vulnerable = Math.Max(vulnerable, SpecialCardEffectHeuristics.GetSpecialEnemyVulnerableAmount(card));
        weak = Math.Max(weak, SpecialCardEffectHeuristics.GetSpecialEnemyWeakAmount(card));
        int selfStrength = card.GetSelfStrengthAmount();
        int selfTemporaryStrength = card.GetSelfTemporaryStrengthAmount();
        int selfDexterity = card.GetSelfDexterityAmount();
        int selfTemporaryDexterity = card.GetSelfTemporaryDexterityAmount();
        int specialSetupScore = SpecialCardEffectHeuristics.ScoreLineSetup(context, action, card);
        bool isHighVariance = cardsDrawn > context.KnownDrawPileTopCards.Count;
        bool isDebuffPotion = IsDebuffPotion(action);
        bool isDirectDamagePotion = IsDirectDamagePotion(action);
        bool isBuffPotion = IsBuffPotion(action);
        bool isResourcePotion = IsResourcePotion(action);
        bool isScalingPotion = IsScalingPotion(action);
        bool isHandFixPotion = IsHandFixPotion(action);
        bool isThornsPotion = IsThornsPotion(action);
        bool isDuplicationPotion = IsDuplicationPotion(action);
        bool isPersistentDefensePotion = IsPersistentDefensePotion(action);
        bool isOffensivePotion = isDebuffPotion || isDirectDamagePotion || IsAttackGeneratingPotion(action);
        bool appliesVulnerable = vulnerable > 0;
        bool damagesAllEnemies = card.DealsDamageToAllEnemies();
        bool appliesVulnerableToAllEnemies = card.AppliesVulnerableToAllEnemies();
        bool appliesWeakToAllEnemies = card.AppliesWeakToAllEnemies();
        bool isLowConfidenceNoBenefitCard = CombatActionScorer.IsLowConfidenceNoBenefitCard(card);
        bool usefulEnergyFollowUp = damage > 0 ||
                                    block > 0 ||
                                    summonProtection > 0 ||
                                    cardsDrawn > 0 ||
                                    vulnerable > 0 ||
                                    weak > 0 ||
                                    selfStrength > 0 ||
                                    selfTemporaryStrength > 0 ||
                                    selfDexterity > 0 ||
                                    selfTemporaryDexterity > 0 ||
                                    specialSetupScore > 0 ||
                                    SpecialCardEffectHeuristics.HasKnownSpecialBenefit(card) ||
                                    card?.Type == CardType.Attack ||
                                    card?.Type == CardType.Power;

        if (isDebuffPotion && vulnerable <= 0 && IsVulnerablePotion(action))
        {
            vulnerable = 1;
            appliesVulnerable = true;
        }

        if (isDebuffPotion && weak <= 0 && IsWeakPotion(action))
        {
            weak = 1;
        }

        ApplyPotionSetupEstimates(action, ref energyGain, ref selfStrength, ref selfTemporaryStrength, ref selfDexterity, ref selfTemporaryDexterity);
        bool isHandCleanup = StatusCardStrategy.IsLikelyHandCleanupCard(card);
        int handCleanupTargets = isHandCleanup
            ? StatusCardStrategy.CountAllowedHandCleanupTargets(context.HandCardsByInstanceId.Values, context.Actor)
            : 0;

        return new PlannableAction
        {
            Action = action,
            ImmediateScore = immediateScore,
            EnergyCost = action.EnergyCost ?? 0,
            StarCost = starCost,
            Damage = damage,
            DamageHits = Math.Max(GetDamageHits(card) + evoke.DamageHits, damage > 0 ? 1 : 0),
            PunishableAttackHits = card?.Type == CardType.Attack && directDamage > 0
                ? Math.Max(1, GetDamageHits(card))
                : 0,
            StrengthScalingHits = Math.Max(0, card.GetDirectDamageHits()),
            Block = block,
            SummonProtection = summonProtection,
            CardsDrawn = cardsDrawn,
            KnownBadDraws = knownBadDraws,
            IsHandCleanup = isHandCleanup,
            IsUnusableHandCleanup = isHandCleanup && handCleanupTargets <= 0,
            HandCleanupScore = EstimateHandCleanupScore(context, card),
            SpecialSetupScore = specialSetupScore,
            EnergyGain = energyGain,
            StarsGenerated = starsGenerated,
            Vulnerable = vulnerable,
            Weak = weak,
            SelfStrength = Math.Max(0, selfStrength - selfTemporaryStrength),
            SelfTemporaryStrength = selfTemporaryStrength,
            SelfDexterity = Math.Max(0, selfDexterity - selfTemporaryDexterity),
            SelfTemporaryDexterity = selfTemporaryDexterity,
            IsHighVariance = isHighVariance,
            IsEndTurn = string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal),
            IsOffensivePotion = isOffensivePotion,
            IsSetupPotion = isDebuffPotion || isBuffPotion || isResourcePotion || isScalingPotion || isHandFixPotion || isThornsPotion || isDuplicationPotion || isPersistentDefensePotion,
            IsResourcePotion = isResourcePotion,
            IsEnemyDebuff = vulnerable > 0 || weak > 0 || isDebuffPotion,
            AppliesVulnerable = appliesVulnerable,
            DamagesAllEnemies = damagesAllEnemies,
            AppliesVulnerableToAllEnemies = appliesVulnerableToAllEnemies || SpecialCardEffectHeuristics.AppliesSpecialDebuffToAllEnemies(card),
            AppliesWeakToAllEnemies = appliesWeakToAllEnemies || SpecialCardEffectHeuristics.AppliesSpecialDebuffToAllEnemies(card),
            IsEnergyOnlyResourceCard = !isPotion &&
                                       energyGain > 0 &&
                                       !usefulEnergyFollowUp,
            IsUsefulEnergyFollowUp = !isPotion && usefulEnergyFollowUp,
            IsLowConfidenceNoBenefitCard = isLowConfidenceNoBenefitCard,
            IsForbiddenLagavulinOpeningAction = LagavulinMatriarchStrategy.IsForbiddenOpeningAction(context, action, card),
            Exhausts = card?.Exhaust == true,
            IsSetup = specialSetupScore > 0 || isDebuffPotion || isBuffPotion || isResourcePotion || isPersistentDefensePotion || immediateScore.Category is CombatActionCategory.PowerSetup or CombatActionCategory.Utility,
            RequiredKnownDrawCount = requiredKnownDrawCount,
            IsExecutableNow = isExecutableNow,
            ConsumptionKey = BuildConsumptionKey(action)
        };
    }

    private static bool ShouldConsiderLineAction(PlannableAction action)
    {
        if (action.IsEndTurn)
        {
            return false;
        }

        return true;
    }

    private static bool CanDamageNonMinionEnemy(DeterministicCombatContext context, PlannableAction action)
    {
        if (action.Damage <= 0)
        {
            return false;
        }

        if (action.DamagesAllEnemies)
        {
            return context.EnemiesById.Values.Any(static enemy => !enemy.IsLikelySummonedAdd);
        }

        return !string.IsNullOrEmpty(action.Action.TargetId) &&
               context.EnemiesById.TryGetValue(action.Action.TargetId, out DeterministicEnemyState? enemy) &&
               !enemy.IsLikelySummonedAdd;
    }

    private IEnumerable<PlannableAction> BuildKnownDrawPlannableActions(DeterministicCombatContext context)
    {
        for (int drawIndex = 0; drawIndex < context.KnownDrawPileTopCards.Count; drawIndex++)
        {
            ResolvedCardView card = context.KnownDrawPileTopCards[drawIndex];
            if (StatusCardStrategy.IsNegativeStatusOrCurse(card))
            {
                continue;
            }

            foreach (AiLegalActionOption action in BuildVirtualPlayActionsForDrawnCard(context, card, drawIndex))
            {
                yield return BuildPlannableAction(context, action, card, drawIndex + 1, isExecutableNow: false);
            }
        }
    }

    private static IEnumerable<AiLegalActionOption> BuildVirtualPlayActionsForDrawnCard(
        DeterministicCombatContext context,
        ResolvedCardView card,
        int drawIndex)
    {
        string actionPrefix = $"virtual_draw_{drawIndex}_{SanitizeActionToken(card.CardId)}";
        if (card.Targeting == TargetType.AnyEnemy)
        {
            foreach (string enemyId in context.EnemiesById.Keys.OrderBy(static enemyId => enemyId, StringComparer.Ordinal))
            {
                yield return BuildVirtualPlayAction(card, $"{actionPrefix}_target_{enemyId}", enemyId);
            }

            yield break;
        }

        yield return BuildVirtualPlayAction(card, $"{actionPrefix}_target_none", "none");
    }

    private static AiLegalActionOption BuildVirtualPlayAction(ResolvedCardView card, string actionId, string targetId)
    {
        return new AiLegalActionOption
        {
            ActionId = actionId,
            ActionType = AiTeammateActionKind.PlayCard.ToString(),
            Description = $"Known draw play {card.CardId}",
            Label = $"Known draw {card.CardId}",
            Summary = $"Simulated play after drawing {card.CardId}.",
            CardId = card.CardId,
            CardInstanceId = card.CardInstanceId,
            TargetId = targetId,
            TargetLabel = targetId,
            EnergyCost = Math.Max(0, card.EffectiveCost)
        };
    }

    private static ResolvedCardView? ResolveCard(DeterministicCombatContext context, AiLegalActionOption action)
    {
        if (!string.IsNullOrEmpty(action.CardInstanceId) &&
            context.HandCardsByInstanceId.TryGetValue(action.CardInstanceId, out ResolvedCardView? card))
        {
            return card;
        }

        return null;
    }

    private static int GetDamageHits(ResolvedCardView? card)
    {
        if (card == null)
        {
            return 1;
        }

        return card.GetDirectDamageHits();
    }

    private static string BuildConsumptionKey(AiLegalActionOption action)
    {
        if (action.ActionId.StartsWith("virtual_draw_", StringComparison.Ordinal))
        {
            int targetIndex = action.ActionId.IndexOf("_target_", StringComparison.Ordinal);
            string virtualUseId = targetIndex > 0
                ? action.ActionId[..targetIndex]
                : action.ActionId;
            return $"virtual:{virtualUseId}";
        }

        if (!string.IsNullOrEmpty(action.CardInstanceId))
        {
            return $"card:{action.CardInstanceId}";
        }

        if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
        {
            int targetIndex = action.ActionId.IndexOf("_target_", StringComparison.Ordinal);
            string potionUseId = targetIndex > 0
                ? action.ActionId[..targetIndex]
                : action.ActionId;
            return $"potion:{potionUseId}";
        }

        return $"action:{action.ActionId}";
    }

    private static string SanitizeActionToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }

    private static bool IsOffensivePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BINDING", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("VULNERABLE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("WEAK", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("POISON", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DOOM", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FIRE", StringComparison.OrdinalIgnoreCase)
               || IsAttackGeneratingPotion(action);
    }

    private static bool IsDebuffPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BINDING", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("VULNERABLE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("WEAK", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("POISON", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DOOM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectDamagePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("FIRE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("EXPLOSIVE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("AMPOULE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuffPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("STRENGTH", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FLEX", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DEXTERITY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("SPEED", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FYSH_OIL", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FISH_OIL", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("FOCUS", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("CAPACITY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("CULTIST", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("STABLE_SERUM", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("BLESSING_OF_THE_FORGE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsResourcePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("ENERGY", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("DRAW", StringComparison.OrdinalIgnoreCase)
               || IsHandFixPotion(action);
    }

    private static bool IsVulnerablePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("VULNERABLE", StringComparison.OrdinalIgnoreCase)
               || potionId.Contains("BINDING", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeakPotion(AiLegalActionOption action)
    {
        return (action.CardId ?? string.Empty).Contains("WEAK", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScalingPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("FOCUS", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("CAPACITY", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("CULTIST", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryStrengthPotion(AiLegalActionOption action)
    {
        return (action.CardId ?? string.Empty).Contains("FLEX", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryDexterityPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("SPEED", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("FYSH_OIL", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("FISH_OIL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHandFixPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("GAMBL", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("SWIFT", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("SKILL_POTION", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("ATTACK_POTION", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("POWER_POTION", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("COLORLESS", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("LIQUID_MEMORIES", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("ENTROPIC", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("STABLE_SERUM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThornsPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("LIQUID_BRONZE", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("BRONZE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDuplicationPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("DUPLICATOR", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("DUPLICATION", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImmediateBlockPotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("BLOCK", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("ARMOR", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimateImmediateBlockPotionAmount(AiLegalActionOption action)
    {
        if (!IsImmediateBlockPotion(action))
        {
            return 0;
        }

        string potionId = action.CardId ?? string.Empty;
        if (potionId.Contains("BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            return 12;
        }

        return 10;
    }

    private static bool IsPersistentDefensePotion(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        return potionId.Contains("HEART_OF_IRON", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("METALLIC", StringComparison.OrdinalIgnoreCase) ||
               potionId.Contains("REGEN", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActorTarget(DeterministicCombatContext context, AiLegalActionOption action)
    {
        return string.Equals(action.TargetId, $"player_{context.Actor.NetId}", StringComparison.Ordinal) ||
               string.Equals(action.TargetId, "none", StringComparison.Ordinal) ||
               string.IsNullOrEmpty(action.TargetId);
    }

    private static bool IsAttackGeneratingPotion(AiLegalActionOption action)
    {
        return (action.CardId ?? string.Empty).Contains("ATTACK_POTION", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimateDirectPotionDamage(AiLegalActionOption action)
    {
        string potionId = action.CardId ?? string.Empty;
        if (potionId.Contains("FIRE", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        if (potionId.Contains("EXPLOSIVE", StringComparison.OrdinalIgnoreCase) ||
            potionId.Contains("AMPOULE", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return 0;
    }

    private static int EstimateHandCleanupScore(DeterministicCombatContext context, ResolvedCardView? card)
    {
        if (!StatusCardStrategy.IsLikelyHandCleanupCard(card))
        {
            return 0;
        }

        int safeTargets = StatusCardStrategy.CountAllowedHandCleanupTargets(context.HandCardsByInstanceId.Values, context.Actor);
        if (safeTargets <= 0)
        {
            return -5000;
        }

        int score = Math.Min(safeTargets, 3) * 42;
        score += Math.Min(120, (int)Math.Round(StatusCardStrategy.SumAllowedCleanupBurden(context.HandCardsByInstanceId.Values, context.Actor) * 0.8d));
        if (context.IncomingDamageAfterBlock > 0 || context.IsEliteOrBossCombat)
        {
            score += Math.Min(safeTargets, 3) * 10;
        }

        if (StatusCardStrategy.IsUnsafeWholeHandCleanupCard(card))
        {
            score -= context.IsEliteOrBossCombat ? 260 : 180;
        }

        return score;
    }

    private static void ApplyPotionSetupEstimates(
        AiLegalActionOption action,
        ref int energyGain,
        ref int selfStrength,
        ref int selfTemporaryStrength,
        ref int selfDexterity,
        ref int selfTemporaryDexterity)
    {
        string potionId = action.CardId ?? string.Empty;
        if (potionId.Contains("ENERGY", StringComparison.OrdinalIgnoreCase))
        {
            energyGain = Math.Max(energyGain, 2);
        }

        if (potionId.Contains("STRENGTH", StringComparison.OrdinalIgnoreCase))
        {
            selfStrength = Math.Max(selfStrength, 2);
        }

        if (potionId.Contains("FLEX", StringComparison.OrdinalIgnoreCase))
        {
            selfTemporaryStrength = Math.Max(selfTemporaryStrength, 2);
        }

        if (potionId.Contains("DEXTERITY", StringComparison.OrdinalIgnoreCase))
        {
            selfDexterity = Math.Max(selfDexterity, 2);
        }

        if (potionId.Contains("SPEED", StringComparison.OrdinalIgnoreCase))
        {
            selfTemporaryDexterity = Math.Max(selfTemporaryDexterity, 2);
        }

        if (potionId.Contains("FYSH_OIL", StringComparison.OrdinalIgnoreCase) ||
            potionId.Contains("FISH_OIL", StringComparison.OrdinalIgnoreCase))
        {
            selfTemporaryDexterity = Math.Max(selfTemporaryDexterity, 5);
        }
    }

    private sealed class PlannableAction
    {
        public required AiLegalActionOption Action { get; init; }

        public required CombatActionScore ImmediateScore { get; init; }

        public required string ConsumptionKey { get; init; }

        public int EnergyCost { get; init; }

        public int StarCost { get; init; }

        public int Damage { get; init; }

        public int DamageHits { get; init; }

        public int PunishableAttackHits { get; init; }

        public int StrengthScalingHits { get; init; }

        public int Block { get; init; }

        public int SummonProtection { get; init; }

        public int CardsDrawn { get; init; }

        public int KnownBadDraws { get; init; }

        public bool IsHandCleanup { get; init; }

        public bool IsUnusableHandCleanup { get; init; }

        public int HandCleanupScore { get; init; }

        public int SpecialSetupScore { get; init; }

        public int EnergyGain { get; init; }

        public int StarsGenerated { get; init; }

        public int Vulnerable { get; init; }

        public int Weak { get; init; }

        public int SelfStrength { get; init; }

        public int SelfTemporaryStrength { get; init; }

        public int SelfDexterity { get; init; }

        public int SelfTemporaryDexterity { get; init; }

        public bool IsHighVariance { get; init; }

        public bool IsEndTurn { get; init; }

        public bool IsOffensivePotion { get; init; }

        public bool IsSetupPotion { get; init; }

        public bool IsResourcePotion { get; init; }

        public bool IsEnemyDebuff { get; init; }

        public bool IsEnergyOnlyResourceCard { get; init; }

        public bool IsUsefulEnergyFollowUp { get; init; }

        public bool IsLowConfidenceNoBenefitCard { get; init; }

        public bool IsForbiddenLagavulinOpeningAction { get; init; }

        public bool Exhausts { get; init; }

        public bool AppliesVulnerable { get; init; }

        public bool DamagesAllEnemies { get; init; }

        public bool AppliesVulnerableToAllEnemies { get; init; }

        public bool AppliesWeakToAllEnemies { get; init; }

        public bool IsSetup { get; init; }

        public int RequiredKnownDrawCount { get; init; }

        public bool IsExecutableNow { get; init; } = true;
    }

    private sealed class LineNode
    {
        private readonly HashSet<string> _consumedKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _damageByTargetId = new(StringComparer.Ordinal);
        private readonly HashSet<string> _deadEnemyIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ShadowEnemyState> _enemyStates = new(StringComparer.Ordinal);

        public LineNode(int energyRemaining, int starsRemaining)
        {
            EnergyRemaining = energyRemaining;
            StarsRemaining = starsRemaining;
        }

        private LineNode(LineNode other)
        {
            EnergyRemaining = other.EnergyRemaining;
            StarsRemaining = other.StarsRemaining;
            ActionIds = other.ActionIds.ToList();
            _consumedKeys = new HashSet<string>(other._consumedKeys, StringComparer.Ordinal);
            _damageByTargetId = new Dictionary<string, int>(other._damageByTargetId, StringComparer.Ordinal);
            _deadEnemyIds = new HashSet<string>(other._deadEnemyIds, StringComparer.Ordinal);
            _enemyStates = other._enemyStates.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Clone(),
                StringComparer.Ordinal);
            LastSingleEnemyTargetId = other.LastSingleEnemyTargetId;
	            BaseScore = other.BaseScore;
	            TotalDamageDealt = other.TotalDamageDealt;
	            TotalBlockGained = other.TotalBlockGained;
            TotalSummonProtection = other.TotalSummonProtection;
	            TeamBlockPrevented = other.TeamBlockPrevented;
	            SetupScore = other.SetupScore;
            DamagePreventedByKills = other.DamagePreventedByKills;
            DamagePreventedByWeak = other.DamagePreventedByWeak;
            AttackPunishDamageTaken = other.AttackPunishDamageTaken;
            StrengthGained = other.StrengthGained;
            TemporaryStrengthGained = other.TemporaryStrengthGained;
            DexterityGained = other.DexterityGained;
            TemporaryDexterityGained = other.TemporaryDexterityGained;
            CardsDrawn = other.CardsDrawn;
            KnownBadDraws = other.KnownBadDraws;
            HandCleanupScore = other.HandCleanupScore;
            EnergyGenerated = other.EnergyGenerated;
            StarsGenerated = other.StarsGenerated;
            StopExpanding = other.StopExpanding;
        }

        public int EnergyRemaining { get; private set; }

        public int StarsRemaining { get; private set; }

        public List<string> ActionIds { get; } = [];

        public int BaseScore { get; private set; }

        public int TotalDamageDealt { get; private set; }

	        public int TotalBlockGained { get; private set; }

        public int TotalSummonProtection { get; private set; }

	        public int TeamBlockPrevented { get; private set; }

        public int SetupScore { get; private set; }

        public int DamagePreventedByKills { get; private set; }

        public int DamagePreventedByWeak { get; private set; }

        public int AttackPunishDamageTaken { get; private set; }

        public int StrengthGained { get; private set; }

        public int TemporaryStrengthGained { get; private set; }

        public int DexterityGained { get; private set; }

        public int TemporaryDexterityGained { get; private set; }

        public int CardsDrawn { get; private set; }

        public int KnownBadDraws { get; private set; }

        public int HandCleanupScore { get; private set; }

        public int EnergyGenerated { get; private set; }

        public int StarsGenerated { get; private set; }

        public bool StopExpanding { get; private set; }

        public string? LastSingleEnemyTargetId { get; private set; }

        public bool HasPreparedVulnerableTarget(DeterministicCombatContext context, PlannableAction action)
        {
            EnsureShadowEnemies(context);
            foreach (ShadowEnemyState target in ResolveShadowEnemyTargets(context, action, action.DamagesAllEnemies))
            {
                if (target.HasVulnerable)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanApply(
            DeterministicCombatContext? context,
            PlannableAction action,
            IReadOnlyList<PlannableAction>? allActions = null)
        {
            if (!action.IsExecutableNow && ActionIds.Count == 0)
            {
                return false;
            }

            if (action.RequiredKnownDrawCount > 0 && CardsDrawn < action.RequiredKnownDrawCount)
            {
                return false;
            }

            if (_consumedKeys.Contains(action.ConsumptionKey))
            {
                return false;
            }

            if (action.EnergyCost > EnergyRemaining)
            {
                return false;
            }

            if (action.StarCost > StarsRemaining)
            {
                return false;
            }

            if (context != null &&
                action.IsUnusableHandCleanup)
            {
                return false;
            }

            if (context != null &&
                action.IsForbiddenLagavulinOpeningAction)
            {
                return false;
            }

            if (context != null &&
                action.Damage > 0 &&
                !HasLiveDamageTarget(context, action))
            {
                return false;
            }

            if (context != null &&
                action.IsSetupPotion &&
                !HasUsefulFollowUpAfter(context, action))
            {
                return false;
            }

            if (context != null &&
                action.IsEnergyOnlyResourceCard &&
                !HasEnergyUnlockedUsefulFollowUpAfter(action, allActions))
            {
                return false;
            }

            if (context != null &&
                action.IsLowConfidenceNoBenefitCard &&
                (context.HasCatastrophicEnemyAction ||
                 (context.IsEliteOrBossCombat && (context.HasSustainedAttackPressure || context.IncomingDamageAfterBlock > 0))))
            {
                return false;
            }

            if (context != null &&
                context.IsLagavulinMatriarchOpeningSetupWindow &&
                WouldDamageSleepingLagavulinMatriarch(context, action))
            {
                return false;
            }

            return true;
        }

        public LineNode Apply(DeterministicCombatContext context, PlannableAction action)
        {
            AiCombatCoreWeights core = context.CombatConfig.Combat.CoreWeights;
            AiCombatStatusWeights status = context.CombatConfig.Combat.StatusWeights;
            AiCombatResourceWeights resource = context.CombatConfig.Combat.ResourceWeights;
            LineNode next = new(this)
            {
                EnergyRemaining = Math.Max(0, EnergyRemaining - action.EnergyCost + action.EnergyGain),
                StarsRemaining = Math.Max(0, StarsRemaining - action.StarCost + action.StarsGenerated)
            };
            next.EnsureShadowEnemies(context);
            next.ActionIds.Add(action.Action.ActionId);
            next._consumedKeys.Add(action.ConsumptionKey);
            if (!string.IsNullOrEmpty(action.Action.TargetId) &&
                !action.DamagesAllEnemies &&
                next._enemyStates.TryGetValue(action.Action.TargetId, out ShadowEnemyState? lastTarget) &&
                !lastTarget.IsDead)
            {
                next.LastSingleEnemyTargetId = action.Action.TargetId;
            }

            next.BaseScore += action.ImmediateScore.TotalScore / Math.Max(1, ImmediateActionBiasDivisor);
            next.EnergyGenerated += action.EnergyGain;
            next.StarsGenerated += action.StarsGenerated;
            next.CardsDrawn += action.CardsDrawn;
            next.KnownBadDraws += action.KnownBadDraws;
            next.HandCleanupScore += action.HandCleanupScore;

	            int effectiveBlock = action.Block + next.DexterityGained + next.TemporaryDexterityGained;
            int effectiveSummonProtection = action.SummonProtection;
            int effectiveProtection = effectiveBlock + effectiveSummonProtection;
	            if (effectiveProtection > 0)
	            {
	                DeterministicPlayerState? blockTarget = ResolvePlayerTarget(context, action.Action);
	                if (blockTarget == null || blockTarget.IsActor)
	                {
	                    next.TotalBlockGained += effectiveBlock;
                    next.TotalSummonProtection += effectiveSummonProtection;
	                }
	                else
	                {
	                    int prevented = Math.Min(effectiveProtection, blockTarget.IncomingDamageAfterBlock);
	                    next.TeamBlockPrevented += prevented;
	                    next.SetupScore += prevented * 10;
	                    if (blockTarget.IsInGraveDanger)
	                    {
	                        next.SetupScore += 90;
	                    }
	                }
	            }

            foreach (ShadowEnemyState target in next.ResolveShadowEnemyTargets(context, action, action.AppliesVulnerableToAllEnemies || action.AppliesWeakToAllEnemies))
            {
                if (action.AppliesVulnerable)
                {
                    target.ApplyVulnerable();
                }

                if (action.Weak > 0)
                {
                    if (target.ApplyWeak(out int preventedDamage))
                    {
                        next.DamagePreventedByWeak += preventedDamage;
                    }
                }
            }

            if (action.IsEnemyDebuff)
            {
                int futureDamageActions = CountAffordableUnconsumedActions(next, context, requireDamage: true);
                int debuffSetupScore = action.Vulnerable * (futureDamageActions > 0 ? 36 : 10) +
                                       action.Weak * (context.IncomingDamage > 0 ? 28 : 10);
                if (action.AppliesVulnerableToAllEnemies || action.AppliesWeakToAllEnemies)
                {
                    debuffSetupScore += Math.Min(context.EnemiesById.Count, 4) * 16;
                }

                if (ActionIds.Count == 0)
                {
                    debuffSetupScore += EnemyDebuffOpeningLineBonus;
                }
                else if (TotalDamageDealt == 0)
                {
                    debuffSetupScore += EnemyDebuffEarlyLineBonus;
                }
                else
                {
                    debuffSetupScore -= EnemyDebuffAfterDamagePenalty;
                }

                if (action.Vulnerable > 0 && futureDamageActions == 0)
                {
                    debuffSetupScore -= 30;
                }

                next.SetupScore += debuffSetupScore;
            }

            if (action.Damage > 0)
            {
                foreach (ShadowEnemyState target in next.ResolveShadowEnemyTargets(context, action, action.DamagesAllEnemies))
                {
                    int dealtDamage = action.Damage + (next.StrengthGained + next.TemporaryStrengthGained) * action.StrengthScalingHits;
                    if (target.HasVulnerable)
                    {
                        dealtDamage += (int)Math.Ceiling(dealtDamage * 0.5m);
                    }

                    int effectiveDamage = CombatActionScorer.EstimateEffectiveDamageAgainstEnemy(target.Source, dealtDamage, action.DamageHits);
                    int usefulDamage = target.ApplyEffectiveDamage(effectiveDamage, out bool killedEnemy);
                    next.TotalDamageDealt += usefulDamage;
                    int wastedDamage = Math.Max(0, effectiveDamage - usefulDamage);
                    if (wastedDamage > 0)
                    {
                        int overkillPenaltyPerPoint = Math.Max(core.LineDamageValuePerPoint, core.DirectDamageValuePerPoint / 2);
                        next.SetupScore -= wastedDamage * overkillPenaltyPerPoint;
                    }

                    if (action.PunishableAttackHits > 0 && target.Source.PunishesAttacks)
                    {
                        next.AttackPunishDamageTaken += target.Source.PunishingAttackAmount * action.PunishableAttackHits;
                        if (!target.IsDead)
                        {
                            next.SetupScore -= ThornsNonLethalLinePenalty;
                        }
                    }

                    next._damageByTargetId[target.Id] = next._damageByTargetId.GetValueOrDefault(target.Id) + usefulDamage;
                    if (killedEnemy && next._deadEnemyIds.Add(target.Id))
                    {
                        next.DamagePreventedByWeak = Math.Max(0, next.DamagePreventedByWeak - target.WeakPreventionApplied);
                        next.DamagePreventedByKills += EstimateEnemyRemovalPrevention(target.Source, context.EnemiesById.Count);
                    }
                }
            }

            next.StrengthGained += action.SelfStrength;
            next.TemporaryStrengthGained += action.SelfTemporaryStrength;
            next.DexterityGained += action.SelfDexterity;
            next.TemporaryDexterityGained += action.SelfTemporaryDexterity;

            if (action.IsSetup)
            {
                next.SetupScore += resource.SetupActionBonus;
            }

            if (context.IsLagavulinMatriarchOpeningSetupWindow &&
                action.Damage <= 0 &&
                (action.IsSetup || action.IsEnemyDebuff || action.SpecialSetupScore > 0))
            {
                next.SetupScore += LagavulinSleepSetupLineBonus;
            }

            if (action.SelfStrength > 0 || action.SelfTemporaryStrength > 0)
            {
                next.SetupScore += CountAffordableUnconsumedActions(next, context, requireDamage: true) * (action.SelfStrength * status.SetupPersistentStrengthValue + action.SelfTemporaryStrength * status.SetupTemporaryStrengthValue);
            }

            if (action.SelfDexterity > 0 || action.SelfTemporaryDexterity > 0)
            {
                next.SetupScore += CountAffordableUnconsumedActions(next, context, requireBlock: true) * (action.SelfDexterity * status.SetupPersistentDexterityValue + action.SelfTemporaryDexterity * status.SetupTemporaryDexterityValue);
            }

            if (action.CardsDrawn > 0)
            {
                int futurePlayableActions = CountAffordableUnconsumedActions(next, context);
                int usefulDraws = Math.Max(0, action.CardsDrawn - action.KnownBadDraws);
                if (next.EnergyRemaining > 0 && futurePlayableActions > 0)
                {
                    next.SetupScore += usefulDraws * resource.SetupDrawValueWhenPlayable;
                }
                else
                {
                    next.SetupScore -= usefulDraws * resource.SetupDrawPenaltyWhenNotPlayable;
                }

                if (action.KnownBadDraws > 0)
                {
                    int badDrawPenalty = action.IsHandCleanup
                        ? resource.SetupDrawPenaltyWhenNotPlayable
                        : resource.SetupDrawPenaltyWhenNotPlayable + 30;
                    next.SetupScore -= action.KnownBadDraws * badDrawPenalty;
                }
            }

            if (action.HandCleanupScore > 0)
            {
                next.SetupScore += action.HandCleanupScore;
            }

            if (action.SpecialSetupScore > 0)
            {
                next.SetupScore += action.SpecialSetupScore;
            }

            if (action.EnergyGain > 0)
            {
                next.SetupScore += action.EnergyGain * resource.SetupEnergyGainValue;
                if (action.IsEnergyOnlyResourceCard && action.Exhausts)
                {
                    next.SetupScore -= 40;
                }
            }

            if (action.StarsGenerated > 0)
            {
                next.SetupScore += action.StarsGenerated * 10;
                if (CountAffordableUnconsumedActions(next, context) > 0)
                {
                    next.SetupScore += action.StarsGenerated * 8;
                }
            }

            next.StopExpanding = action.IsHighVariance || next.ActionIds.Count >= MaxLineLength;
            return next;
        }

        private bool HasUsefulFollowUpAfter(DeterministicCombatContext context, PlannableAction setupAction)
        {
            int energyAfter = Math.Max(0, EnergyRemaining - setupAction.EnergyCost + setupAction.EnergyGain);
            int starsAfter = Math.Max(0, StarsRemaining - setupAction.StarCost + setupAction.StarsGenerated);
            bool requireDamageFollowUp = IsTemporaryStrengthPotion(setupAction.Action);
            bool requireBlockFollowUp = IsTemporaryDexterityPotion(setupAction.Action);
            if (requireBlockFollowUp && context.TeamIncomingDamageAfterBlock <= 0)
            {
                return false;
            }

            if (IsScalingPotion(setupAction.Action) && IsLongFight(context))
            {
                return true;
            }

            if (IsHandFixPotion(setupAction.Action) && (HasPoorHand(context) || context.IsEliteOrBossCombat))
            {
                return true;
            }

            if (IsThornsPotion(setupAction.Action) && context.IncomingDamage > 0)
            {
                return true;
            }

            if (IsPersistentDefensePotion(setupAction.Action) &&
                (context.IsEliteOrBossCombat || context.HasSustainedAttackPressure || context.IncomingDamageAfterBlock > 0))
            {
                return true;
            }

            return context.LegalActions.Any(candidate =>
            {
                if (_consumedKeys.Contains(BuildConsumptionKey(candidate)) ||
                    string.Equals(candidate.ActionId, setupAction.Action.ActionId, StringComparison.Ordinal) ||
                    string.Equals(candidate.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                    string.Equals(candidate.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                    (candidate.EnergyCost ?? 0) > energyAfter)
                {
                    return false;
                }

                ResolvedCardView? card = ResolveCard(context, candidate);
                if ((card?.StarCost ?? 0) > starsAfter)
                {
                    return false;
                }

                if (setupAction.IsResourcePotion && !requireDamageFollowUp && !requireBlockFollowUp)
                {
                    return true;
                }

                bool dealsDamage = card?.HasEffect(EffectKind.DealDamage) == true || card?.Type == CardType.Attack;
                bool gainsBlock = card?.HasEffect(EffectKind.GainBlock) == true || card?.HasEffect(EffectKind.Summon) == true;
                if (requireDamageFollowUp)
                {
                    return dealsDamage;
                }

                if (requireBlockFollowUp)
                {
                    return gainsBlock;
                }

                return dealsDamage || gainsBlock;
            });
        }

        private static bool IsLongFight(DeterministicCombatContext context)
        {
            return context.IsEliteOrBossCombat ||
                   context.HasSustainedAttackPressure ||
                   TotalEnemyEffectiveHp(context) >= 70;
        }

        private static bool HasPoorHand(DeterministicCombatContext context)
        {
            if (StatusCardStrategy.CountNegativeStatusOrCurse(context.HandCardsByInstanceId.Values) >= 2)
            {
                return true;
            }

            int playableNonPotion = context.LegalActions.Count(action =>
            {
                if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                    string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                    (action.EnergyCost ?? 0) > context.Energy)
                {
                    return false;
                }

                return (ResolveCard(context, action)?.StarCost ?? 0) <= context.Stars;
            });
            if (playableNonPotion <= 0)
            {
                return true;
            }

            bool hasAttack = context.LegalActions.Any(action =>
            {
                if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                    string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                    (action.EnergyCost ?? 0) > context.Energy)
                {
                    return false;
                }

                ResolvedCardView? card = ResolveCard(context, action);
                if ((card?.StarCost ?? 0) > context.Stars)
                {
                    return false;
                }

                return card?.HasEffect(EffectKind.DealDamage) == true || card?.Type == CardType.Attack;
            });
            bool hasBlock = context.LegalActions.Any(action =>
            {
                if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) ||
                    string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                    (action.EnergyCost ?? 0) > context.Energy)
                {
                    return false;
                }

                ResolvedCardView? card = ResolveCard(context, action);
                return (card?.StarCost ?? 0) <= context.Stars &&
                       (card?.HasEffect(EffectKind.GainBlock) == true || card?.HasEffect(EffectKind.Summon) == true);
            });

            if (context.IncomingDamageAfterBlock > 0 && !hasBlock)
            {
                return true;
            }

            return TotalEnemyEffectiveHp(context) > 0 && !hasAttack && !hasBlock;
        }

        private static int TotalEnemyEffectiveHp(DeterministicCombatContext context)
        {
            return context.EnemiesById.Values.Sum(static enemy => Math.Max(0, enemy.CurrentHp + enemy.Block));
        }

        private bool HasEnergyUnlockedUsefulFollowUpAfter(
            PlannableAction setupAction,
            IReadOnlyList<PlannableAction>? allActions)
        {
            if (allActions == null)
            {
                return false;
            }

            int energyWithoutGain = Math.Max(0, EnergyRemaining - setupAction.EnergyCost);
            int energyWithGain = energyWithoutGain + Math.Max(0, setupAction.EnergyGain);
            return allActions.Any(candidate =>
                !_consumedKeys.Contains(candidate.ConsumptionKey) &&
                !string.Equals(candidate.ConsumptionKey, setupAction.ConsumptionKey, StringComparison.Ordinal) &&
                !string.Equals(candidate.Action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal) &&
                !candidate.IsEndTurn &&
                IsKnownDrawRequirementMet(candidate) &&
                candidate.StarCost <= StarsRemaining &&
                candidate.EnergyCost > energyWithoutGain &&
                candidate.EnergyCost <= energyWithGain &&
                candidate.IsUsefulEnergyFollowUp);
        }

        private bool WouldDamageSleepingLagavulinMatriarch(DeterministicCombatContext context, PlannableAction action)
        {
            if (action.Damage <= 0)
            {
                return false;
            }

            EnsureShadowEnemies(context);
            foreach (ShadowEnemyState target in ResolveShadowEnemyTargets(context, action, action.DamagesAllEnemies))
            {
                if (!target.Source.IsLagavulinMatriarchAsleep)
                {
                    continue;
                }

                int dealtDamage = action.Damage + (StrengthGained + TemporaryStrengthGained) * action.StrengthScalingHits;
                if (target.HasVulnerable)
                {
                    dealtDamage += (int)Math.Ceiling(dealtDamage * 0.5m);
                }

                dealtDamage = CombatActionScorer.EstimateEffectiveDamageAgainstEnemy(target.Source, dealtDamage, action.DamageHits);
                if (dealtDamage > 0)
                {
                    return true;
                }
            }

            return false;
        }

	        private static DeterministicPlayerState? ResolvePlayerTarget(DeterministicCombatContext context, AiLegalActionOption action)
	        {
	            if (!string.IsNullOrEmpty(action.TargetId) &&
	                context.PlayerStatesById.TryGetValue(action.TargetId, out DeterministicPlayerState? target))
	            {
	                return target;
	            }

	            if (!string.IsNullOrEmpty(action.TargetId) &&
	                !string.Equals(action.TargetId, "none", StringComparison.Ordinal))
	            {
	                return null;
	            }

	            string actorTargetId = $"player_{context.Actor.NetId}";
	            return context.PlayerStatesById.TryGetValue(actorTargetId, out DeterministicPlayerState? actorTarget)
	                ? actorTarget
	                : null;
	        }

        private bool HasLiveDamageTarget(DeterministicCombatContext context, PlannableAction action)
        {
            EnsureShadowEnemies(context);
            return ResolveShadowEnemyTargets(context, action, action.DamagesAllEnemies).Any();
        }

        private void EnsureShadowEnemies(DeterministicCombatContext context)
        {
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in context.EnemiesById)
            {
                if (_enemyStates.ContainsKey(enemy.Key))
                {
                    continue;
                }

                ShadowEnemyState shadow = new(enemy.Key, enemy.Value);
                _enemyStates[enemy.Key] = shadow;
                if (shadow.IsDead)
                {
                    _deadEnemyIds.Add(enemy.Key);
                }
            }
        }

        private IEnumerable<ShadowEnemyState> ResolveShadowEnemyTargets(
            DeterministicCombatContext context,
            PlannableAction action,
            bool allEnemies)
        {
            EnsureShadowEnemies(context);
            if (allEnemies)
            {
                foreach (string enemyId in context.EnemiesById.Keys)
                {
                    if (_enemyStates.TryGetValue(enemyId, out ShadowEnemyState? enemy) &&
                        !enemy.IsDead)
                    {
                        yield return enemy;
                    }
                }

                yield break;
            }

            if (!string.IsNullOrEmpty(action.Action.TargetId) &&
                _enemyStates.TryGetValue(action.Action.TargetId, out ShadowEnemyState? target) &&
                !target.IsDead)
            {
                yield return target;
                yield break;
            }

            if (action.Damage <= 0)
            {
                yield break;
            }

            ShadowEnemyState? onlyAliveEnemy = null;
            foreach (string enemyId in context.EnemiesById.Keys)
            {
                if (!_enemyStates.TryGetValue(enemyId, out ShadowEnemyState? enemy) ||
                    enemy.IsDead)
                {
                    continue;
                }

                if (onlyAliveEnemy != null)
                {
                    yield break;
                }

                onlyAliveEnemy = enemy;
            }

            if (onlyAliveEnemy != null)
            {
                yield return onlyAliveEnemy;
            }
        }

        private int CountAffordableUnconsumedActions(LineNode node, DeterministicCombatContext context, bool requireDamage = false, bool requireBlock = false)
        {
            return context.LegalActions.Count(action =>
            {
                if (string.IsNullOrEmpty(action.ActionId) ||
                    node._consumedKeys.Contains(BuildConsumptionKey(action)) ||
                    string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal) ||
                    (action.EnergyCost ?? 0) > node.EnergyRemaining)
                {
                    return false;
                }

                ResolvedCardView? card = ResolveCard(context, action);
                if ((card?.StarCost ?? 0) > node.StarsRemaining)
                {
                    return false;
                }

                if (requireDamage)
                {
                    return card?.HasEffect(EffectKind.DealDamage) == true;
                }

                if (requireBlock)
                {
                    return card?.HasEffect(EffectKind.GainBlock) == true || card?.HasEffect(EffectKind.Summon) == true;
                }

                return true;
            });
        }

        public int EstimatedDamageTaken(DeterministicCombatContext context)
        {
            int incomingDamage = Math.Max(0, context.IncomingDamage - DamagePreventedByKills - DamagePreventedByWeak);
            int availableProtection = context.CurrentBlock + TotalBlockGained + TotalSummonProtection;
            return Math.Max(0, incomingDamage - availableProtection) + AttackPunishDamageTaken;
        }

        public int EstimatedBlockAfterEnemyTurn(DeterministicCombatContext context)
        {
            int incomingDamage = Math.Max(0, context.IncomingDamage - DamagePreventedByKills - DamagePreventedByWeak);
            int blockBeforeSummon = context.CurrentBlock + TotalBlockGained;
            int leftoverBlock = Math.Max(0, blockBeforeSummon - incomingDamage);
            int summonDamage = Math.Max(0, incomingDamage - blockBeforeSummon);
            int leftoverSummon = Math.Max(0, TotalSummonProtection - summonDamage);
            return (context.HasBlockRetention ? leftoverBlock : 0) + leftoverSummon;
        }

        public int ComputeTerminalScore(DeterministicCombatContext context, IReadOnlyList<PlannableAction> actions)
        {
            AiCharacterCombatTuning tuning = context.CombatConfig.Combat;
            AiCombatCoreWeights core = tuning.CoreWeights;
            AiCombatStatusWeights status = tuning.StatusWeights;
            AiCombatResourceWeights resource = tuning.ResourceWeights;
            AiCombatRiskProfile risk = tuning.RiskProfile;
            int incomingDamage = Math.Max(0, context.IncomingDamage - DamagePreventedByKills - DamagePreventedByWeak);
            int availableProtection = context.CurrentBlock + TotalBlockGained + TotalSummonProtection;
            int attackPunishDamage = AttackPunishDamageTaken;
            int damageTaken = Math.Max(0, incomingDamage - availableProtection) + attackPunishDamage;
            int preventedByBlock = Math.Min(incomingDamage, availableProtection);
            int leftoverBlock = EstimatedBlockAfterEnemyTurn(context);
            int remainingAffordableActions = actions.Count(action =>
                !_consumedKeys.Contains(action.ConsumptionKey) &&
                IsKnownDrawRequirementMet(action) &&
                !action.IsEndTurn &&
                action.EnergyCost <= EnergyRemaining &&
                action.StarCost <= StarsRemaining);

	            int score = BaseScore;
	            score += risk.ApplySurvivalWeight(preventedByBlock * risk.PreventedDamageValuePerPoint);
	            score += risk.ApplyDefenseWeight(TeamBlockPrevented * risk.PreventedDamageValuePerPoint);
            score -= risk.ApplySurvivalWeight(damageTaken * risk.DamageTakenPenaltyPerPoint);
            score -= risk.ApplySurvivalWeight(attackPunishDamage * ThornsReturnedDamageTerminalPenaltyPerPoint);
            score += DamagePreventedByKills * risk.KillPreventionValuePerPoint;
            score += DamagePreventedByWeak * risk.WeakPreventionValuePerPoint;
            score += risk.ApplyAttackWeight(TotalDamageDealt * core.LineDamageValuePerPoint);
            score += SetupScore;
            score += risk.ApplyDefenseWeight(leftoverBlock * core.LeftoverBlockValuePerPoint);
            score += _deadEnemyIds.Count * risk.DeadEnemyReward;
            score += StrengthGained * status.LinePersistentStrengthValue;
            score += TemporaryStrengthGained * status.LineTemporaryStrengthValue;
            score += DexterityGained * status.LinePersistentDexterityValue;
            score += TemporaryDexterityGained * (incomingDamage > 0 ? status.LineTemporaryDexterityThreatenedValue : status.LineTemporaryDexteritySafeValue);
            score += EnergyGenerated * resource.LineEnergyGeneratedValue;
            if (EnergyGenerated > 0 && EnergyRemaining > 0 && remainingAffordableActions == 0)
            {
                score -= Math.Min(EnergyGenerated, EnergyRemaining) * resource.LineEnergyGeneratedValue * 3;
            }

            int usefulCardsDrawn = Math.Max(0, CardsDrawn - KnownBadDraws);
            score += usefulCardsDrawn * (remainingAffordableActions > 0 ? resource.LineCardsDrawnValueWhenUsable : -resource.LineCardsDrawnPenaltyWhenNotUsable);
            score -= KnownBadDraws * (resource.LineCardsDrawnPenaltyWhenNotUsable + 28);
            score -= EnergyRemaining * resource.RemainingEnergyPenalty;
            score -= remainingAffordableActions * resource.RemainingAffordableActionsPenalty;
            score += EstimateCatastrophicRaceTerminalScore(context, core, risk, damageTaken, preventedByBlock);
            score += EstimateSustainedAttackRaceTerminalScore(context, core, risk, damageTaken, preventedByBlock);
            score += EstimatePrimaryTargetTerminalScore(context);
            score += EstimateNonMinionLethalTerminalScore(context);
            score += EstimateLagavulinSleepTerminalScore(context);
            score += EstimateKaiserCrabFinalFacingScore(context);
            if (context.EnemiesById.Count > 0 && _deadEnemyIds.Count >= context.EnemiesById.Count)
            {
                score += AllEnemiesLethalLineBonus;
            }
            else if (damageTaken >= context.CurrentHp)
            {
                score -= LethalTurnWithoutKillPenalty;
                score += Math.Min(650, TotalDamageDealt * 6);
                score += _deadEnemyIds.Count * 260;
            }
            else if (context.IsTeamInCrisis && damageTaken > 0)
            {
                score -= Math.Min(TeamCrisisDamageTakenPenaltyCap, damageTaken * 35);
            }

            if (damageTaken == 0 && preventedByBlock > 0)
            {
                score += risk.PerfectDefenseBonus;
            }

            if (damageTaken > 0 && TotalBlockGained == 0 && DamagePreventedByWeak == 0)
            {
                score -= risk.ExposedDamageWithoutDefensePenalty;
            }

            return score;
        }

        private int EstimateLagavulinSleepTerminalScore(DeterministicCombatContext context)
        {
            if (!context.IsLagavulinMatriarchOpeningSetupWindow)
            {
                return 0;
            }

            int score = 0;
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in context.EnemiesById.Where(static pair => pair.Value.IsLagavulinMatriarchAsleep))
            {
                int damage = _damageByTargetId.GetValueOrDefault(enemy.Key);
                int block = Math.Max(0, enemy.Value.Block);
                if (damage > block)
                {
                    score -= LagavulinSleepWakeLinePenalty + Math.Min(6000, (damage - block) * 180);
                }
                else if (damage > 0)
                {
                    score -= damage * LagavulinSleepBlockChipLinePenaltyPerPoint;
                }
            }

            return score;
        }

        public bool KillsAllEnemies(DeterministicCombatContext context)
        {
            return context.EnemiesById.Count > 0 && _deadEnemyIds.Count >= context.EnemiesById.Count;
        }

        public bool KillsAllNonMinionEnemies(DeterministicCombatContext context)
        {
            int nonMinionCount = 0;
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in context.EnemiesById)
            {
                if (enemy.Value.IsLikelySummonedAdd)
                {
                    continue;
                }

                nonMinionCount++;
                if (!_deadEnemyIds.Contains(enemy.Key))
                {
                    return false;
                }
            }

            return nonMinionCount > 0;
        }

        private int EstimateNonMinionLethalTerminalScore(DeterministicCombatContext context)
        {
            if (!KillsAllNonMinionEnemies(context))
            {
                return 0;
            }

            int nonMinionKilled = context.EnemiesById.Count(pair => !pair.Value.IsLikelySummonedAdd && _deadEnemyIds.Contains(pair.Key));
            int score = NonMinionLethalLineBonus + nonMinionKilled * 1200;
            if (KillsAllEnemies(context))
            {
                return score;
            }

            int minionDamage = 0;
            int minionKills = 0;
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in context.EnemiesById.Where(static pair => pair.Value.IsLikelySummonedAdd))
            {
                int effectiveHp = Math.Max(1, enemy.Value.CurrentHp + enemy.Value.Block);
                minionDamage += Math.Min(_damageByTargetId.GetValueOrDefault(enemy.Key), effectiveHp);
                if (_deadEnemyIds.Contains(enemy.Key))
                {
                    minionKills++;
                }
            }

            score -= Math.Min(2400, minionDamage * NonMinionLethalMinionDamagePenaltyPerPoint);
            score -= minionKills * NonMinionLethalMinionKillPenalty;
            return score;
        }

        private int EstimateKaiserCrabFinalFacingScore(DeterministicCombatContext context)
        {
            if (!context.IsKaiserCrabCombat || string.IsNullOrEmpty(LastSingleEnemyTargetId))
            {
                return 0;
            }

            KeyValuePair<string, DeterministicEnemyState>? preferredTarget = context.EnemiesById
                .Where(pair => pair.Value.IsKaiserCrabPart &&
                               pair.Value.IncomingDamage > 0 &&
                               !_deadEnemyIds.Contains(pair.Key))
                .OrderByDescending(static pair => pair.Value.IncomingDamage)
                .ThenBy(static pair => pair.Value.CurrentHp + pair.Value.Block)
                .FirstOrDefault();
            if (preferredTarget == null || string.IsNullOrEmpty(preferredTarget.Value.Key))
            {
                return 0;
            }

            if (string.Equals(LastSingleEnemyTargetId, preferredTarget.Value.Key, StringComparison.Ordinal))
            {
                return KaiserCrabFinalFacingTargetBonus + Math.Min(360, preferredTarget.Value.Value.IncomingDamage * 16);
            }

            if (context.EnemiesById.TryGetValue(LastSingleEnemyTargetId, out DeterministicEnemyState? lastTarget) &&
                lastTarget.IsKaiserCrabPart)
            {
                int damageGap = Math.Max(0, preferredTarget.Value.Value.IncomingDamage - lastTarget.IncomingDamage);
                return -(KaiserCrabFinalFacingWrongTargetPenalty + Math.Min(420, damageGap * 18));
            }

            return -KaiserCrabFinalFacingWrongTargetPenalty / 2;
        }

        private int EstimateCatastrophicRaceTerminalScore(
            DeterministicCombatContext context,
            AiCombatCoreWeights core,
            AiCombatRiskProfile risk,
            int damageTaken,
            int preventedByBlock)
        {
            if (!context.HasCatastrophicEnemyAction)
            {
                return 0;
            }

            if (context.IsWaterfallSelfDestructDefenseWindow)
            {
                int defenseScore = preventedByBlock * 82;
                if (damageTaken > 0)
                {
                    defenseScore -= Math.Min(2200, damageTaken * 115);
                }

                if (damageTaken >= context.CurrentHp)
                {
                    defenseScore -= 1800;
                }

                return defenseScore;
            }

            IEnumerable<KeyValuePair<string, DeterministicEnemyState>> threatEntries = context.EnemiesById
                .Where(static pair => pair.Value.HasCatastrophicMove);
            if (!threatEntries.Any())
            {
                threatEntries = context.EnemiesById;
            }

            int usefulDamageToThreats = 0;
            int killedThreats = 0;
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in threatEntries)
            {
                int effectiveHp = Math.Max(1, enemy.Value.CurrentHp + enemy.Value.Block);
                int dealt = _damageByTargetId.GetValueOrDefault(enemy.Key);
                usefulDamageToThreats += Math.Min(dealt, effectiveHp);
                if (_deadEnemyIds.Contains(enemy.Key))
                {
                    killedThreats++;
                }
            }

            int score = risk.ApplyAttackWeight(usefulDamageToThreats * (core.LineDamageValuePerPoint + 18));
            if (usefulDamageToThreats <= 0 && preventedByBlock > 0)
            {
                score -= CatastrophicNoProgressPenalty;
            }

            score += killedThreats * CatastrophicThreatKillBonus;

            if (damageTaken >= context.CurrentHp)
            {
                score += Math.Min(1800, usefulDamageToThreats * 22);
                score += killedThreats * 420;
            }

            return score;
        }

        private int EstimatePrimaryTargetTerminalScore(DeterministicCombatContext context)
        {
            DeterministicTeamCombatTactics tactics = context.TeamTactics;
            bool targetLock = (context.IsPhantasmalGardenersCombat || context.IsObscuraCombat || tactics.IsTargetLock) && tactics.HasPrimaryTarget;
            if (!targetLock && !tactics.HasFocusedKill)
            {
                return 0;
            }

            string primaryTargetId = tactics.PrimaryTargetId;
            if (!context.EnemiesById.TryGetValue(primaryTargetId, out DeterministicEnemyState? primaryEnemy))
            {
                return 0;
            }

            int primaryDamage = _damageByTargetId.GetValueOrDefault(primaryTargetId);
            if (primaryDamage <= 0)
            {
                return targetLock
                    ? context.IsObscuraCombat ? -ObscuraNoPrimaryProgressPenalty : -GardenersNoPrimaryProgressPenalty
                    : 0;
            }

            int effectiveHp = Math.Max(1, primaryEnemy.CurrentHp + primaryEnemy.Block);
            int usefulDamage = Math.Min(primaryDamage, effectiveHp);
            int score = targetLock
                ? usefulDamage * (context.IsObscuraCombat ? ObscuraPrimaryDamageLineValue : GardenersPrimaryDamageLineValue)
                : usefulDamage * 8;

            if (_deadEnemyIds.Contains(primaryTargetId))
            {
                score += targetLock
                    ? context.IsObscuraCombat ? ObscuraPrimaryKillLineBonus : GardenersPrimaryKillLineBonus
                    : 360;
            }
            else if (targetLock)
            {
                int highestOffTargetDamage = _damageByTargetId
                    .Where(pair => !string.Equals(pair.Key, primaryTargetId, StringComparison.Ordinal))
                    .Select(static pair => pair.Value)
                    .DefaultIfEmpty(0)
                    .Max();
                if (highestOffTargetDamage > primaryDamage)
                {
                    int offTargetPenalty = context.IsObscuraCombat ? ObscuraOffTargetLinePenalty : GardenersOffTargetLinePenalty;
                    score -= offTargetPenalty + Math.Min(context.IsObscuraCombat ? 680 : 280, (highestOffTargetDamage - primaryDamage) * 10);
                }

                if (primaryEnemy.CurrentHp <= 30)
                {
                    score += Math.Max(45, 190 - primaryEnemy.CurrentHp * 4);
                }
            }

            return score;
        }

        private int EstimateSustainedAttackRaceTerminalScore(
            DeterministicCombatContext context,
            AiCombatCoreWeights core,
            AiCombatRiskProfile risk,
            int damageTaken,
            int preventedByBlock)
        {
            int pressure = context.SustainedAttackPressure;
            if (pressure <= 0)
            {
                return 0;
            }

            int totalThreatHp = 0;
            int usefulDamageToThreats = 0;
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in context.EnemiesById)
            {
                bool summonedAddPressure = enemy.Value.IsLikelySummonedAdd &&
                                           (enemy.Value.IncomingDamage > 0 || context.IsTeamInCrisis);
                if (enemy.Value.SustainedAttackPressure <= 0 && !summonedAddPressure)
                {
                    continue;
                }

                int effectiveHp = enemy.Value.CurrentHp + enemy.Value.Block;
                totalThreatHp += effectiveHp;
                int usefulDamage = Math.Min(_damageByTargetId.GetValueOrDefault(enemy.Key), effectiveHp);
                usefulDamageToThreats += usefulDamage;
                if (summonedAddPressure && _deadEnemyIds.Contains(enemy.Key))
                {
                    usefulDamageToThreats += Math.Min(18, enemy.Value.IncomingDamage * 2 + 8);
                }
            }

            if (totalThreatHp <= 0)
            {
                return 0;
            }

            int pressureDamageValue = core.LineDamageValuePerPoint + Math.Clamp(pressure / 24, 3, 9);
            int score = risk.ApplyAttackWeight(usefulDamageToThreats * pressureDamageValue);
            int hpAfterLine = context.CurrentHp - damageTaken;
            int safeReserve = CombatActionScorer.EstimateSustainedAttackRaceHpReserve(context);
            if (hpAfterLine > safeReserve && damageTaken > 0)
            {
                int expendableDamage = Math.Min(damageTaken, hpAfterLine - safeReserve);
                score += risk.ApplySurvivalWeight(expendableDamage * risk.DamageTakenPenaltyPerPoint / 2);
            }

            if (hpAfterLine > safeReserve)
            {
                int expectedProgress = Math.Min(totalThreatHp, Math.Max(6, pressure / 12));
                if (usefulDamageToThreats < expectedProgress)
                {
                    int missingProgress = expectedProgress - usefulDamageToThreats;
                    score -= missingProgress * Math.Clamp(pressure / 18, 4, 10);
                }

                if (damageTaken == 0 && preventedByBlock > 0 && usefulDamageToThreats == 0)
                {
                    score -= Math.Min(180, pressure);
                }
            }

            return score;
        }

        private static int EstimateEnemyRemovalPrevention(DeterministicEnemyState enemy, int enemyCount)
        {
            int prevention = enemy.IncomingDamage;
            if (enemyCount > 1)
            {
                prevention += Math.Min(35, Math.Max(0, enemy.SustainedAttackPressure / 4));
            }

            if (enemyCount > 2)
            {
                prevention += 6;
            }

            return prevention;
        }

        private bool IsKnownDrawRequirementMet(PlannableAction action)
        {
            return action.RequiredKnownDrawCount <= 0 || CardsDrawn >= action.RequiredKnownDrawCount;
        }
    }

    private sealed class ShadowEnemyState
    {
        public ShadowEnemyState(string id, DeterministicEnemyState source)
        {
            Id = id;
            Source = source;
            CurrentHp = Math.Max(0, source.CurrentHp);
            Block = Math.Max(0, source.Block);
            HasVulnerable = source.HasVulnerable;
            HasWeak = source.HasWeak;
        }

        private ShadowEnemyState(ShadowEnemyState other)
        {
            Id = other.Id;
            Source = other.Source;
            CurrentHp = other.CurrentHp;
            Block = other.Block;
            HasVulnerable = other.HasVulnerable;
            HasWeak = other.HasWeak;
            WeakPreventionApplied = other.WeakPreventionApplied;
        }

        public string Id { get; }

        public DeterministicEnemyState Source { get; }

        public int CurrentHp { get; private set; }

        public int Block { get; private set; }

        public bool HasVulnerable { get; private set; }

        public bool HasWeak { get; private set; }

        public int WeakPreventionApplied { get; private set; }

        public bool IsDead => CurrentHp <= 0;

        public ShadowEnemyState Clone()
        {
            return new ShadowEnemyState(this);
        }

        public void ApplyVulnerable()
        {
            HasVulnerable = true;
        }

        public bool ApplyWeak(out int preventedDamage)
        {
            if (HasWeak || Source.IncomingDamage <= 0)
            {
                HasWeak = true;
                preventedDamage = 0;
                return false;
            }

            HasWeak = true;
            preventedDamage = Math.Max(1, Source.IncomingDamage / 4);
            WeakPreventionApplied += preventedDamage;
            return true;
        }

        public int ApplyEffectiveDamage(int effectiveDamage, out bool killedEnemy)
        {
            killedEnemy = false;
            if (IsDead)
            {
                return 0;
            }

            int before = EffectiveHp;
            if (effectiveDamage <= 0)
            {
                return 0;
            }

            int remainingDamage = effectiveDamage;
            int blockDamage = Math.Min(Block, remainingDamage);
            Block -= blockDamage;
            remainingDamage -= blockDamage;
            if (remainingDamage > 0)
            {
                CurrentHp = Math.Max(0, CurrentHp - remainingDamage);
            }

            killedEnemy = CurrentHp <= 0;
            return Math.Max(0, before - EffectiveHp);
        }

        private int EffectiveHp => Math.Max(0, CurrentHp) + Math.Max(0, Block);
    }
}
