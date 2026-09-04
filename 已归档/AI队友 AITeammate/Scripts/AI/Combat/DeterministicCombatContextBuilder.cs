using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace AITeammate.Scripts;

internal sealed class DeterministicCombatContextBuilder
{
    private readonly ICardResolver _cardResolver = new CardResolver(
        CardCatalogRepository.Shared,
        new CardDefinitionRepository(),
        new RunCardStateStore(),
        new CombatCardStateStore());

    public DeterministicCombatContext? Build(string actorId, IReadOnlyList<AiLegalActionOption> legalActions)
    {
        if (!ulong.TryParse(actorId, out ulong parsedActorId))
        {
            return null;
        }

        Player? player = RunManager.Instance.DebugOnlyGetState()?.GetPlayer(parsedActorId);
        if (player?.Creature?.CombatState == null || player.PlayerCombatState == null)
        {
            return null;
        }

        AbstractRoom? currentRoom = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom;
        string roomTypeName = currentRoom != null
            ? $"{currentRoom.RoomType}:{currentRoom.GetType().Name}"
            : "UnknownRoom";
        string encounterId = TryGetCurrentEncounterId(currentRoom);
        FuturePotionRewardPreview potionRewardPreview = currentRoom != null
            ? FutureRewardOracle.Shared.PreviewPotionRewardAfterCurrentCombat(player, currentRoom.RoomType)
            : FuturePotionRewardPreview.Empty;

        Dictionary<string, ResolvedCardView> handCardsByInstanceId = PileType.Hand.GetPile(player).Cards
            .GroupBy(GetCardInstanceId)
            .ToDictionary(
                group => group.Key,
                group => _cardResolver.Resolve(group.First(), group.Key),
                StringComparer.Ordinal);
        IReadOnlyList<ResolvedCardView> knownDrawPileTopCards = ResolveKnownDrawPileTopCards(player);

        HashSet<Creature> enemiesDeadBeforeEnemyTurn = EstimateEnemiesDeadBeforeEnemyTurn(player.Creature.CombatState);
        List<Creature> hittableEnemies = player.Creature.CombatState.HittableEnemies
            .Where(enemy => !enemiesDeadBeforeEnemyTurn.Contains(enemy))
            .ToList();
        Dictionary<string, DeterministicEnemyState> enemiesById = new(StringComparer.Ordinal);
        foreach (Creature enemy in hittableEnemies)
        {
            int enemyDamage = EstimateIncomingAttackDamageBeforeBlock(enemy, player.Creature);
            string enemyId = GetTargetId(enemy);
	            enemiesById[enemyId] = new DeterministicEnemyState
	            {
	                Id = enemyId,
	                Creature = enemy,
	                PowerAmounts = CollectVisiblePowerAmounts(enemy),
	                IncomingDamage = enemyDamage,
	                NextMoveId = enemy.Monster?.NextMove?.StateId ?? string.Empty,
	                MonsterId = enemy.Monster?.Id.Entry ?? enemy.GetType().Name
	            };
	        }

        Dictionary<string, DeterministicPlayerState> playerStatesById = BuildPlayerStates(player, hittableEnemies);
        DeterministicPlayerState? actorState = playerStatesById.Values.FirstOrDefault(static state => state.IsActor);

	        Dictionary<string, int> actorPowerAmounts = CollectVisiblePowerAmounts(player.Creature);
	        bool isPhantasmalGardenersCombat = PhantasmalGardenersStrategy.IsGardenersCombat(encounterId, enemiesById);
	        bool isKaiserCrabCombat = IsKaiserCrabCombat(encounterId, enemiesById);
	        bool isObscuraCombat = ObscuraStrategy.IsObscuraCombat(encounterId, enemiesById);

        HashSet<string> actorRelicIds = player.Relics
            .Select(static relic => relic.Id.Entry.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        DeterministicTeamCombatTactics teamTactics = BuildTeamCombatTactics(
            player,
            legalActions,
            handCardsByInstanceId,
            enemiesById,
            encounterId,
            isPhantasmalGardenersCombat,
            isObscuraCombat);

        return new DeterministicCombatContext
        {
            Actor = player,
            LegalActions = legalActions,
            HandCardsByInstanceId = handCardsByInstanceId,
            EnemiesById = enemiesById,
            PlayerStatesById = playerStatesById,
            KnownDrawPileTopCards = knownDrawPileTopCards,
            ActorPowerAmounts = actorPowerAmounts,
            ActorRelicIds = actorRelicIds,
            CombatConfig = AiCharacterCombatConfigLoader.LoadForPlayer(player),
            RoomTypeName = roomTypeName,
            EncounterId = encounterId,
            IsEliteCombat = IsRoomKind(currentRoom, "Elite"),
	            IsBossCombat = IsRoomKind(currentRoom, "Boss"),
	            IsPhantasmalGardenersCombat = isPhantasmalGardenersCombat,
	            IsKaiserCrabCombat = isKaiserCrabCombat,
	            IsObscuraCombat = isObscuraCombat,
            CombatRoundNumber = player.Creature.CombatState.RoundNumber,
            FuturePotionDropAfterCombat = potionRewardPreview.WillDrop,
            FuturePotionDropDescription = potionRewardPreview.Describe(),
            IncomingDamage = actorState?.IncomingDamage ?? 0,
            ExpectedBlockAtEnemyTurn = actorState?.ExpectedBlockAtEnemyTurn ?? player.Creature.Block,
            UnblockableIncomingDamage = actorState?.UnblockableIncomingDamage ?? 0,
            PredictedIncomingDamageAfterBlock = actorState?.PredictedIncomingDamageAfterBlock,
            TeamTactics = teamTactics
        };
    }

    private DeterministicTeamCombatTactics BuildTeamCombatTactics(
        Player actor,
        IReadOnlyList<AiLegalActionOption> legalActions,
        IReadOnlyDictionary<string, ResolvedCardView> actorHandCardsByInstanceId,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById,
        string encounterId,
        bool isPhantasmalGardenersCombat,
        bool isObscuraCombat)
    {
        if (enemiesById.Count == 0 || actor.Creature.CombatState == null)
        {
            return DeterministicTeamCombatTactics.Empty;
        }

        IReadOnlyList<IEncounterTargetingStrategy> encounterTargetingStrategies =
            EncounterTargetingStrategyRegistry.GetActive(encounterId, enemiesById, isPhantasmalGardenersCombat, isObscuraCombat);
        bool forceTargetLock = encounterTargetingStrategies.Any(static strategy => strategy.RequiresTargetLock);

        Dictionary<string, int> teamDamageByEnemyId = enemiesById.Keys.ToDictionary(static id => id, static _ => 0, StringComparer.Ordinal);
        Dictionary<string, int> actorDamageByEnemyId = enemiesById.Keys.ToDictionary(static id => id, static _ => 0, StringComparer.Ordinal);
        int teamDamageTotal = 0;
        int actorDamageTotal = 0;

        foreach (Creature playerCreature in actor.Creature.CombatState.PlayerCreatures.Where(static creature => creature.IsAlive))
        {
            Player? player = playerCreature.Player;
            if (player?.PlayerCombatState == null)
            {
                continue;
            }

            Dictionary<string, int> playerDamageByEnemyId = ReferenceEquals(player, actor)
                ? EstimateLegalActionDamageByEnemy(actor, legalActions, actorHandCardsByInstanceId, enemiesById)
                : EstimateLiveHandDamageByEnemy(player, enemiesById);

            foreach (KeyValuePair<string, int> damage in playerDamageByEnemyId)
            {
                if (!teamDamageByEnemyId.ContainsKey(damage.Key))
                {
                    continue;
                }

                teamDamageByEnemyId[damage.Key] += damage.Value;
                if (ReferenceEquals(player, actor))
                {
                    actorDamageByEnemyId[damage.Key] += damage.Value;
                }
            }

            int playerTotalDamage = EstimateTotalDistinctDamage(playerDamageByEnemyId);
            teamDamageTotal += playerTotalDamage;
            if (ReferenceEquals(player, actor))
            {
                actorDamageTotal += playerTotalDamage;
            }
        }

        string primaryTargetId = string.Empty;
        int primaryEffectiveHp = 0;
        int primaryTeamDamage = 0;
        int primaryActorDamage = 0;
        int killableEnemyCount = 0;
        double bestPriority = double.MinValue;
        List<KeyValuePair<string, DeterministicEnemyState>> nonMinionEnemies = enemiesById
            .Where(static pair => !pair.Value.IsLikelySummonedAdd)
            .ToList();
        bool canKillAllNonMinionEnemies = nonMinionEnemies.Count > 0 &&
            nonMinionEnemies.All(enemyEntry =>
            {
                int effectiveHp = Math.Max(1, enemyEntry.Value.CurrentHp + enemyEntry.Value.Block);
                int teamDamage = EstimateDamageAfterEnemyCaps(enemyEntry.Value, teamDamageByEnemyId.GetValueOrDefault(enemyEntry.Key));
                return teamDamage >= effectiveHp;
            });

        foreach (KeyValuePair<string, DeterministicEnemyState> enemyEntry in enemiesById)
        {
            int effectiveHp = Math.Max(1, enemyEntry.Value.CurrentHp + enemyEntry.Value.Block);
	            int teamDamage = EstimateDamageAfterEnemyCaps(enemyEntry.Value, teamDamageByEnemyId.GetValueOrDefault(enemyEntry.Key));
            if (teamDamage >= effectiveHp)
            {
                killableEnemyCount++;
            }

            double? encounterBasePriority = encounterTargetingStrategies
                .Select(strategy => strategy.EstimateBasePriority(enemyEntry.Value, teamDamage, effectiveHp))
                .FirstOrDefault(priority => priority.HasValue);
            double priority = encounterBasePriority ?? EstimateTeamFocusPriority(enemyEntry.Value, teamDamage, effectiveHp);
            priority += encounterTargetingStrategies.Sum(strategy => strategy.EstimatePriorityAdjustment(enemyEntry.Value));

            if (canKillAllNonMinionEnemies && enemyEntry.Value.IsLikelySummonedAdd)
            {
                priority -= 20_000d;
            }

            if (priority > bestPriority)
            {
                bestPriority = priority;
                primaryTargetId = enemyEntry.Key;
                primaryEffectiveHp = effectiveHp;
                primaryTeamDamage = teamDamage;
	                primaryActorDamage = EstimateDamageAfterEnemyCaps(enemyEntry.Value, actorDamageByEnemyId.GetValueOrDefault(enemyEntry.Key));
            }
        }

        bool hasFocusedKill = primaryTeamDamage >= primaryEffectiveHp;
        bool hasTargetLock = forceTargetLock && !string.IsNullOrEmpty(primaryTargetId);
        if (!hasFocusedKill && !hasTargetLock)
        {
            primaryTargetId = string.Empty;
            primaryEffectiveHp = 0;
            primaryTeamDamage = 0;
            primaryActorDamage = 0;
        }

        return new DeterministicTeamCombatTactics
        {
            PrimaryTargetId = primaryTargetId,
            PrimaryTargetEffectiveHp = primaryEffectiveHp,
            EstimatedTeamDamageToPrimary = primaryTeamDamage,
            EstimatedActorDamageToPrimary = primaryActorDamage,
            EstimatedTeamDamageTotal = teamDamageTotal,
            EstimatedActorDamageTotal = actorDamageTotal,
            KillableEnemyCount = killableEnemyCount,
            NonMinionEnemyCount = nonMinionEnemies.Count,
            CanKillAllNonMinionEnemies = canKillAllNonMinionEnemies,
            IsTargetLock = hasTargetLock,
            TeamDamageByEnemyId = teamDamageByEnemyId,
            ActorDamageByEnemyId = actorDamageByEnemyId
        };
    }

    private Dictionary<string, int> EstimateLegalActionDamageByEnemy(
        Player player,
        IReadOnlyList<AiLegalActionOption> legalActions,
        IReadOnlyDictionary<string, ResolvedCardView> handCardsByInstanceId,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        Dictionary<string, List<EstimatedDamageAction>> optionsByEnemyId = enemiesById.Keys
            .ToDictionary(static id => id, static _ => new List<EstimatedDamageAction>(), StringComparer.Ordinal);
        int allEnemyDamage = 0;

        foreach (AiLegalActionOption action in legalActions)
        {
            if (string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            int directPotionDamage = EstimateDirectPotionDamage(action.CardId);
            if (directPotionDamage > 0)
            {
                AddPotionDamageOption(action, directPotionDamage, optionsByEnemyId, ref allEnemyDamage);
                continue;
            }

            if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrEmpty(action.CardInstanceId) ||
                !handCardsByInstanceId.TryGetValue(action.CardInstanceId, out ResolvedCardView? card))
            {
                continue;
            }

            int damage = EstimateCardDamage(player, card);
            if (damage <= 0)
            {
                continue;
            }

            EstimatedDamageAction option = new(
                Damage: damage,
                EnergyCost: Math.Max(0, action.EnergyCost ?? card.EffectiveCost),
                StarCost: Math.Max(0, card.StarCost),
                IsAllEnemies: card.DealsDamageToAllEnemies(),
                SourceKey: action.CardInstanceId);
            AddCardDamageOption(action.TargetId, option, optionsByEnemyId, ref allEnemyDamage);
        }

        return SolveDamageByEnemy(optionsByEnemyId, allEnemyDamage, player.PlayerCombatState?.Energy ?? 0, player.PlayerCombatState?.Stars ?? 0);
    }

    private Dictionary<string, int> EstimateLiveHandDamageByEnemy(
        Player player,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        Dictionary<string, List<EstimatedDamageAction>> optionsByEnemyId = enemiesById.Keys
            .ToDictionary(static id => id, static _ => new List<EstimatedDamageAction>(), StringComparer.Ordinal);
        int allEnemyDamage = 0;

        foreach (CardModel card in PileType.Hand.GetPile(player).Cards)
        {
            if (!CanPlayCard(card))
            {
                continue;
            }

            string cardInstanceId = GetCardInstanceId(card);
            ResolvedCardView resolved = _cardResolver.Resolve(card, cardInstanceId);
            int damage = EstimateCardDamage(player, resolved);
            if (damage <= 0)
            {
                continue;
            }

            EstimatedDamageAction option = new(
                Damage: damage,
                EnergyCost: Math.Max(0, card.EnergyCost.GetAmountToSpend()),
                StarCost: Math.Max(0, resolved.StarCost),
                IsAllEnemies: resolved.DealsDamageToAllEnemies(),
                SourceKey: cardInstanceId);

            if (resolved.DealsDamageToAllEnemies())
            {
                allEnemyDamage += damage;
                continue;
            }

            bool addedTargetedOption = false;
            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in enemiesById)
            {
                if (CanTargetEnemy(card, enemy.Value.Creature))
                {
                    optionsByEnemyId[enemy.Key].Add(option);
                    addedTargetedOption = true;
                }
            }

            if (!addedTargetedOption && resolved.EstimateOrbEvoke(player, player.PlayerCombatState?.Energy ?? 0, option.EnergyCost).Damage > 0)
            {
                AddCardDamageOption(null, option, optionsByEnemyId, ref allEnemyDamage);
            }
        }

        foreach (PotionModel potion in player.Potions.Where(static potion => !potion.IsQueued))
        {
            int directPotionDamage = EstimateDirectPotionDamage(potion.Id.Entry);
            if (directPotionDamage <= 0)
            {
                continue;
            }

            if (!potion.TargetType.IsSingleTarget())
            {
                allEnemyDamage += directPotionDamage;
                continue;
            }

            foreach (KeyValuePair<string, DeterministicEnemyState> enemy in enemiesById)
            {
                if (potion.TargetType == TargetType.AnyEnemy)
                {
                    optionsByEnemyId[enemy.Key].Add(new EstimatedDamageAction(
                        Damage: directPotionDamage,
                        EnergyCost: 0,
                        StarCost: 0,
                        IsAllEnemies: false,
                        SourceKey: potion.Id.Entry));
                }
            }
        }

        return SolveDamageByEnemy(optionsByEnemyId, allEnemyDamage, player.PlayerCombatState?.Energy ?? 0, player.PlayerCombatState?.Stars ?? 0);
    }

    private static Dictionary<string, int> SolveDamageByEnemy(
        IReadOnlyDictionary<string, List<EstimatedDamageAction>> optionsByEnemyId,
        int allEnemyDamage,
        int energy,
        int stars)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<EstimatedDamageAction>> entry in optionsByEnemyId)
        {
            int damage = allEnemyDamage + EstimateBestDamageSubset(entry.Value, energy, stars);
            result[entry.Key] = Math.Max(0, damage);
        }

        return result;
    }

    private static int EstimateBestDamageSubset(IReadOnlyList<EstimatedDamageAction> actions, int energy, int stars)
    {
        Dictionary<(int Energy, int Stars), int> best = new()
        {
            [(Math.Max(0, energy), Math.Max(0, stars))] = 0
        };

        foreach (EstimatedDamageAction action in actions)
        {
            foreach (KeyValuePair<(int Energy, int Stars), int> state in best.ToArray())
            {
                if (action.EnergyCost > state.Key.Energy || action.StarCost > state.Key.Stars)
                {
                    continue;
                }

                (int Energy, int Stars) nextKey = (
                    state.Key.Energy - action.EnergyCost,
                    state.Key.Stars - action.StarCost);
                int nextDamage = state.Value + action.Damage;
                if (!best.TryGetValue(nextKey, out int currentBest) || nextDamage > currentBest)
                {
                    best[nextKey] = nextDamage;
                }
            }
        }

        return best.Values.DefaultIfEmpty(0).Max();
    }

    private static int EstimateCardDamage(Player player, ResolvedCardView card)
    {
        OrbEvokeEstimate evoke = card.EstimateOrbEvoke(
            player,
            player.PlayerCombatState?.Energy ?? 0,
            Math.Max(0, card.EffectiveCost));
        int directDamage = card.GetEstimatedDamage();
        int damage = directDamage + evoke.Damage;
        if (damage <= 0)
        {
            return 0;
        }

        int directHits = card.GetDirectDamageHits();
        int strength = CollectVisiblePowerAmounts(player.Creature)
            .Where(static power => power.Key.Contains("STRENGTH", StringComparison.Ordinal))
            .Sum(static power => power.Value);
        return Math.Max(0, damage + Math.Max(0, strength) * directHits);
    }

    private static void AddCardDamageOption(
        string? targetId,
        EstimatedDamageAction option,
        Dictionary<string, List<EstimatedDamageAction>> optionsByEnemyId,
        ref int allEnemyDamage)
    {
        if (option.IsAllEnemies)
        {
            allEnemyDamage += option.Damage;
            return;
        }

        if (!string.IsNullOrEmpty(targetId) &&
            optionsByEnemyId.TryGetValue(targetId, out List<EstimatedDamageAction>? targetOptions))
        {
            targetOptions.Add(option);
            return;
        }

        if (string.IsNullOrEmpty(targetId) || string.Equals(targetId, "none", StringComparison.Ordinal))
        {
            if (optionsByEnemyId.Count == 1)
            {
                optionsByEnemyId.Values.First().Add(option);
                return;
            }

            int expectedDamage = Math.Max(1, option.Damage / Math.Max(1, optionsByEnemyId.Count));
            foreach (List<EstimatedDamageAction> options in optionsByEnemyId.Values)
            {
                options.Add(option with { Damage = expectedDamage });
            }
        }
    }

    private static void AddPotionDamageOption(
        AiLegalActionOption action,
        int directPotionDamage,
        Dictionary<string, List<EstimatedDamageAction>> optionsByEnemyId,
        ref int allEnemyDamage)
    {
        if (action.CardId?.Contains("EXPLOSIVE", StringComparison.OrdinalIgnoreCase) == true ||
            string.IsNullOrEmpty(action.TargetId) ||
            string.Equals(action.TargetId, "none", StringComparison.Ordinal))
        {
            allEnemyDamage += directPotionDamage;
            return;
        }

        if (optionsByEnemyId.TryGetValue(action.TargetId, out List<EstimatedDamageAction>? targetOptions))
        {
            targetOptions.Add(new EstimatedDamageAction(
                Damage: directPotionDamage,
                EnergyCost: 0,
                StarCost: 0,
                IsAllEnemies: false,
                SourceKey: action.ActionId));
        }
    }

    private static int EstimateTotalDistinctDamage(IReadOnlyDictionary<string, int> damageByEnemyId)
    {
        return damageByEnemyId.Values.DefaultIfEmpty(0).Max();
    }

	    private static double EstimateTeamFocusPriority(DeterministicEnemyState enemy, int teamDamage, int effectiveHp)
    {
        double priority = 0d;
	        if (teamDamage >= effectiveHp)
	        {
	            priority += 10_000d;
	            priority += Math.Min(500, enemy.IncomingDamage * 8 + enemy.SustainedAttackPressure * 3);
	            priority += Math.Max(0, 120 - effectiveHp);
            if (enemy.HasSummonMove)
            {
                priority += 900d;
            }

            if (enemy.IsLikelySummonedAdd)
            {
                priority += 1_050d + Math.Min(650, enemy.IncomingDamage * 14 + enemy.SustainedAttackPressure * 3);
            }

            if (enemy.IsKaiserCrabPart)
            {
                priority += 520d;
            }

	            if (enemy.PunishesAttacks && teamDamage - effectiveHp >= 8)
	            {
	                priority -= 90;
	            }
	        }
	        else
	        {
	            priority += (double)teamDamage / Math.Max(effectiveHp, 1) * 100d;
	            priority += enemy.SustainedAttackPressure;
            if (enemy.HasSummonMove)
            {
                priority += 170d;
            }

            if (enemy.IsLikelySummonedAdd)
            {
                priority += 260d + Math.Min(360, enemy.IncomingDamage * 12 + enemy.SustainedAttackPressure * 2);
            }

            if (enemy.IsKaiserCrabTurnLeverMove && enemy.IncomingDamage <= 0)
            {
                priority += 120d;
	            }
	        }

	        if (enemy.HasIntangible && teamDamage < effectiveHp)
	        {
	            priority -= 360d;
	        }

	        return priority;
	    }

    private static int EstimateDamageAfterEnemyCaps(DeterministicEnemyState enemy, int estimatedDamage)
    {
        if (!enemy.HasIntangible || estimatedDamage <= 0)
        {
            return estimatedDamage;
        }

        return Math.Min(estimatedDamage, 4);
    }

    private static bool IsKaiserCrabCombat(
        string encounterId,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        if (encounterId.Contains("KAISER_CRAB", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return enemiesById.Values.Count(static enemy => enemy.IsKaiserCrabPart) >= 2;
    }

    private static bool CanPlayCard(CardModel card)
    {
        return card.CanPlay(out _, out _);
    }

    private static bool CanTargetEnemy(CardModel card, Creature enemy)
    {
        return card.TargetType == TargetType.AnyEnemy && card.CanPlayTargeting(enemy);
    }

    private static int EstimateDirectPotionDamage(string? potionId)
    {
        if (string.IsNullOrEmpty(potionId))
        {
            return 0;
        }

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

    private static Dictionary<string, DeterministicPlayerState> BuildPlayerStates(Player actor, IReadOnlyList<Creature> hittableEnemies)
    {
        Dictionary<string, DeterministicPlayerState> playersById = new(StringComparer.Ordinal);
        CombatState? combatState = actor.Creature.CombatState;
        if (combatState == null)
        {
            return playersById;
        }

        foreach (Creature creature in combatState.PlayerCreatures.Where(static creature => creature.IsAlive))
        {
            string playerId = GetTargetId(creature);
            IncomingDamageProjection incomingDamage = EstimateIncomingDamageProjection(creature, hittableEnemies, combatState);
            playersById[playerId] = new DeterministicPlayerState
            {
                Id = playerId,
                Creature = creature,
                IsActor = ReferenceEquals(creature, actor.Creature),
                IncomingDamage = incomingDamage.BlockableDamage,
                ExpectedBlockAtEnemyTurn = incomingDamage.ExpectedBlockAtEnemyTurn,
                UnblockableIncomingDamage = incomingDamage.UnblockableDamage,
                PredictedIncomingDamageAfterBlock = incomingDamage.FinalDamageAfterBlock
            };
        }

        return playersById;
    }

    private static IncomingDamageProjection EstimateIncomingDamageProjection(
        Creature target,
        IReadOnlyList<Creature> hittableEnemies,
        CombatState combatState)
    {
        List<int> attackHits = EstimateIncomingAttackHits(target, hittableEnemies, combatState);
        int blockableDamage = attackHits.Sum() + EstimateTurnEndBlockableDamage(target);
        int unblockableDamage = EstimateTurnEndHpLoss(target);
        int expectedBlock = EstimateExpectedBlockAtEnemyTurn(target);
        int finalDamage = ApplyExpectedDamageMitigation(target, attackHits, blockableDamage - attackHits.Sum(), unblockableDamage, expectedBlock);

        return new IncomingDamageProjection(
            BlockableDamage: Math.Max(0, blockableDamage),
            UnblockableDamage: Math.Max(0, finalDamage - Math.Max(0, blockableDamage - expectedBlock)),
            ExpectedBlockAtEnemyTurn: Math.Max(target.Block, expectedBlock),
            FinalDamageAfterBlock: Math.Max(0, finalDamage));
    }

    private static int EstimateIncomingAttackDamageBeforeBlock(Creature enemy, Creature target)
    {
        return EstimateIncomingAttackHitsFromEnemy(enemy, target).Sum();
    }

    private static List<int> EstimateIncomingAttackHits(
        Creature target,
        IReadOnlyList<Creature> hittableEnemies,
        CombatState combatState)
    {
        Creature? interceptor = combatState.PlayerCreatures.FirstOrDefault(static creature =>
            creature.IsAlive &&
            !creature.IsPet &&
            HasPowerToken(creature, "INTERCEPT"));
        if (interceptor != null && !ReferenceEquals(interceptor, target))
        {
            return [];
        }

        List<int> hits = [];
        foreach (Creature enemy in hittableEnemies)
        {
            hits.AddRange(EstimateIncomingAttackHitsFromEnemy(enemy, target));
        }

        return hits;
    }

    private static List<int> EstimateIncomingAttackHitsFromEnemy(Creature enemy, Creature target)
    {
        if (enemy.Monster?.NextMove?.Intents == null)
        {
            return [];
        }

        List<int> hits = [];
        foreach (AttackIntent intent in enemy.Monster.NextMove.Intents.OfType<AttackIntent>())
        {
            int singleDamage = Math.Max(0, intent.GetSingleDamage([target], enemy));
            int totalDamage = Math.Max(0, intent.GetTotalDamage([target], enemy));
            if (singleDamage <= 0 || totalDamage <= 0)
            {
                continue;
            }

            int hitCount = Math.Max(1, totalDamage / singleDamage);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                hits.Add(singleDamage);
            }
        }

        return hits;
    }

    private static int ApplyExpectedDamageMitigation(
        Creature target,
        IReadOnlyList<int> attackHits,
        int extraBlockableDamage,
        int unblockableDamage,
        int expectedBlock)
    {
        int block = Math.Max(0, expectedBlock);
        int petHp = 0;
        int petBlock = 0;
        Creature? livingPet = GetLivingPet(target);
        if (livingPet != null)
        {
            petHp = Math.Max(0, livingPet.CurrentHp);
            petBlock = Math.Max(0, livingPet.Block);
        }

        bool intangible = HasPowerToken(target, "INTANGIBLE", "INCORPOREAL", "WRAITH");
        bool tungstenRod = HasRelicToken(target.Player, "TUNGSTEN_ROD", "TUNGSTEN");
        int hpDamage = 0;

        foreach (int rawHit in attackHits)
        {
            int hit = intangible ? Math.Min(rawHit, 1) : rawHit;
            int blocked = Math.Min(block, hit);
            block -= blocked;
            hit -= blocked;

            if (hit > 0 && petHp > 0)
            {
                int petBlocked = Math.Min(petBlock, hit);
                petBlock -= petBlocked;
                hit -= petBlocked;

                int petDamage = Math.Min(petHp, hit);
                petHp -= petDamage;
                hit -= petDamage;
            }

            if (hit > 0 && tungstenRod)
            {
                hit = Math.Max(0, hit - 1);
            }

            hpDamage += hit;
        }

        foreach (int rawDamage in SplitDamageIntoSingleApplications(extraBlockableDamage))
        {
            int damage = intangible ? Math.Min(rawDamage, 1) : rawDamage;
            int blocked = Math.Min(block, damage);
            block -= blocked;
            damage -= blocked;
            if (damage > 0 && tungstenRod)
            {
                damage = Math.Max(0, damage - 1);
            }

            hpDamage += damage;
        }

        foreach (int rawDamage in SplitDamageIntoSingleApplications(unblockableDamage))
        {
            int damage = intangible ? Math.Min(rawDamage, 1) : rawDamage;
            if (damage > 0 && tungstenRod)
            {
                damage = Math.Max(0, damage - 1);
            }

            hpDamage += damage;
        }

        return Math.Max(0, hpDamage);
    }

    private static IEnumerable<int> SplitDamageIntoSingleApplications(int damage)
    {
        if (damage <= 0)
        {
            yield break;
        }

        yield return damage;
    }

    private static int EstimateExpectedBlockAtEnemyTurn(Creature target)
    {
        int block = Math.Max(0, target.Block);
        Player? player = target.Player;
        if (player != null && block == 0)
        {
            if (HasRelicToken(player, "FAKE_ORICHALCUM"))
            {
                block += 3;
            }
            else if (HasRelicToken(player, "ORICHALCUM"))
            {
                block += 6;
            }
        }

        block += GetPowerAmount(target, "PLATING");
        block += EstimateFrostOrbPassiveBlock(player);

        if (player?.PlayerCombatState?.Hand?.Cards != null && HasRelicToken(player, "CLOAK_CLASP"))
        {
            block += player.PlayerCombatState.Hand.Cards.Count;
        }

        return Math.Max(0, block);
    }

    private static int EstimateTurnEndBlockableDamage(Creature target)
    {
        int total = 0;
        total += GetPowerAmount(target, "CONSTRICT");
        total += GetPowerAmount(target, "MAGIC_BOMB");
        total += GetPowerAmount(target, "DISINTEGRATION");

        Player? player = target.Player;
        if (player?.PlayerCombatState?.Hand?.Cards != null)
        {
            foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
            {
                if (!card.HasTurnEndInHandEffect)
                {
                    continue;
                }

                total += Math.Max(0, GetDynamicVar(card.DynamicVars, "Damage"));
            }
        }

        return Math.Max(0, total);
    }

    private static int EstimateTurnEndHpLoss(Creature target)
    {
        int total = GetPowerAmount(target, "DEMISE");
        Player? player = target.Player;
        if (player?.PlayerCombatState?.Hand?.Cards != null)
        {
            foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
            {
                if (!card.HasTurnEndInHandEffect)
                {
                    continue;
                }

                total += Math.Max(0, GetDynamicVar(card.DynamicVars, "HpLoss"));
            }
        }

        return Math.Max(0, total);
    }

    private static HashSet<Creature> EstimateEnemiesDeadBeforeEnemyTurn(CombatState combatState)
    {
        int accelerant = combatState.PlayerCreatures
            .Where(static creature => creature.IsAlive && creature.IsPlayer && !creature.IsPet)
            .Sum(static creature => GetPowerAmount(creature, "ACCELERANT"));
        HashSet<Creature> fatalPoison = [];
        foreach (Creature enemy in combatState.Enemies.Where(static enemy => enemy.IsAlive && enemy.Monster != null))
        {
            int poison = GetPowerAmount(enemy, "POISON");
            if (poison <= 0)
            {
                continue;
            }

            int hardToKillCap = GetPowerAmount(enemy, "HARD_TO_KILL");
            if (hardToKillCap <= 0)
            {
                hardToKillCap = int.MaxValue;
            }

            int poisonTicks = Math.Min(poison, 1 + accelerant);
            int pendingDamage = 0;
            int tickDamage = poison;
            for (int i = 0; i < poisonTicks; i++)
            {
                pendingDamage += Math.Min(tickDamage, hardToKillCap);
                if (pendingDamage >= enemy.CurrentHp)
                {
                    fatalPoison.Add(enemy);
                    break;
                }

                tickDamage = Math.Max(0, tickDamage - 1);
            }
        }

        if (!combatState.Enemies.Any(enemy => enemy.IsAlive && enemy.Monster != null && enemy.IsPrimaryEnemy && !fatalPoison.Contains(enemy)))
        {
            foreach (Creature secondary in combatState.Enemies.Where(static enemy => enemy.IsAlive && enemy.Monster != null && enemy.IsSecondaryEnemy))
            {
                fatalPoison.Add(secondary);
            }
        }

        return fatalPoison;
    }

    private static Creature? GetLivingPet(Creature owner)
    {
        try
        {
            return owner.Pets?.FirstOrDefault(static pet => pet.IsAlive && !pet.IsDead);
        }
        catch
        {
            return null;
        }
    }

    private static int EstimateFrostOrbPassiveBlock(Player? player)
    {
        if (player?.PlayerCombatState == null)
        {
            return 0;
        }

        IReadOnlyList<object> orbs = GetCurrentOrbs(player).ToList();
        if (orbs.Count == 0)
        {
            return 0;
        }

        bool hasGoldPlatedCables = HasRelicToken(player, "GOLD_PLATED_CABLES", "GOLDPLATEDCABLES");
        int block = 0;
        for (int i = 0; i < orbs.Count; i++)
        {
            object orb = orbs[i];
            if (!HasObjectToken(orb, "FROST"))
            {
                continue;
            }

            int multiplier = hasGoldPlatedCables && i == orbs.Count - 1 ? 2 : 1;
            block += Math.Max(0, ReadIntMember(orb, "PassiveVal", "PassiveValue", "Block")) * multiplier;
        }

        return block;
    }

    private static IEnumerable<object> GetCurrentOrbs(Player player)
    {
        object? combatState = player.PlayerCombatState;
        object? orbs = ReadMember(combatState, "Orbs") ?? ReadMember(combatState, "OrbQueue");
        if (orbs != null && orbs is not string)
        {
            object? nestedOrbs = ReadMember(orbs, "Orbs");
            if (nestedOrbs is System.Collections.IEnumerable nestedEnumerable && nestedOrbs is not string)
            {
                orbs = nestedEnumerable;
            }
        }

        if (orbs is not System.Collections.IEnumerable enumerable || orbs is string)
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

    private static bool HasPowerToken(Creature creature, params string[] tokens)
    {
        return creature.Powers.Any(power => tokens.Any(token =>
            NormalizeToken(power.Id.Entry).Contains(NormalizeToken(token), StringComparison.Ordinal) ||
            NormalizeToken(power.GetType().Name).Contains(NormalizeToken(token), StringComparison.Ordinal)));
    }

    private static int GetPowerAmount(Creature creature, params string[] tokens)
    {
        int amount = 0;
        foreach (PowerModel power in creature.Powers)
        {
            string id = NormalizeToken(power.Id.Entry);
            string typeName = NormalizeToken(power.GetType().Name);
            if (tokens.Any(token =>
                    id.Contains(NormalizeToken(token), StringComparison.Ordinal) ||
                    typeName.Contains(NormalizeToken(token), StringComparison.Ordinal)))
            {
                amount = Math.Max(amount, Math.Max(power.DisplayAmount, 1));
            }
        }

        return amount;
    }

    private static bool HasRelicToken(Player? player, params string[] tokens)
    {
        if (player?.Relics == null)
        {
            return false;
        }

        return player.Relics.Any(relic => tokens.Any(token =>
            NormalizeToken(relic.Id.Entry).Contains(NormalizeToken(token), StringComparison.Ordinal) ||
            NormalizeToken(relic.GetType().Name).Contains(NormalizeToken(token), StringComparison.Ordinal)));
    }

    private static bool HasObjectToken(object value, params string[] tokens)
    {
        string typeName = NormalizeToken(value.GetType().Name);
        string id = NormalizeToken(ReadMember(value, "Id")?.ToString() ?? string.Empty);
        return tokens.Any(token =>
            typeName.Contains(NormalizeToken(token), StringComparison.Ordinal) ||
            id.Contains(NormalizeToken(token), StringComparison.Ordinal));
    }

    private static object? ReadMember(object? value, string name)
    {
        if (value == null)
        {
            return null;
        }

        Type type = value.GetType();
        return type.GetProperty(name)?.GetValue(value) ??
               type.GetField(name)?.GetValue(value);
    }

    private static int ReadIntMember(object value, params string[] names)
    {
        foreach (string name in names)
        {
            object? member = ReadMember(value, name);
            if (member == null)
            {
                continue;
            }

            try
            {
                return Convert.ToInt32(member);
            }
            catch
            {
            }
        }

        return 0;
    }

    private static int GetDynamicVar(IReadOnlyDictionary<string, DynamicVar> dynamicVars, string key)
    {
        return dynamicVars.TryGetValue(key, out DynamicVar? dynamicVar) && dynamicVar != null
            ? (int)dynamicVar.BaseValue
            : 0;
    }

    private static bool IsRoomKind(AbstractRoom? room, string token)
    {
        if (room == null)
        {
            return false;
        }

        return room.RoomType.ToString().Contains(token, StringComparison.OrdinalIgnoreCase) ||
               room.GetType().Name.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string TryGetCurrentEncounterId(AbstractRoom? currentRoom)
    {
        try
        {
            return currentRoom is CombatRoom { Encounter: not null } combatRoom
                ? combatRoom.Encounter.Id.Entry
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private IReadOnlyList<ResolvedCardView> ResolveKnownDrawPileTopCards(Player player)
    {
        IReadOnlyList<CardModel> drawCards = PileType.Draw.GetPile(player).Cards;
        if (drawCards.Count > 0)
        {
            return ResolveKnownDrawCards(drawCards, "draw");
        }

        List<CardModel> shuffledDiscardPreview = PileType.Discard.GetPile(player).Cards.ToList();
        if (shuffledDiscardPreview.Count == 0)
        {
            return [];
        }

        Rng shufflePreview = new(player.RunState.Rng.Shuffle.Seed, player.RunState.Rng.Shuffle.Counter);
        shuffledDiscardPreview.StableShuffle(shufflePreview);
        return ResolveKnownDrawCards(shuffledDiscardPreview, "shuffle");
    }

    private IReadOnlyList<ResolvedCardView> ResolveKnownDrawCards(IReadOnlyList<CardModel> cards, string source)
    {
        return cards
            .Take(5)
            .Select((card, index) => _cardResolver.Resolve(card, $"{source}_{index}_{GetCardInstanceId(card)}"))
            .ToList();
    }

    private static Dictionary<string, int> CollectVisiblePowerAmounts(Creature creature)
    {
        Dictionary<string, int> powerAmounts = new(StringComparer.Ordinal);
        foreach (var power in creature.Powers.Where(static power => power.IsVisible))
        {
            AddPowerToken(powerAmounts, power.Id.Entry, power.DisplayAmount);
            AddPowerToken(powerAmounts, power.GetType().Name, power.DisplayAmount);
        }

        return powerAmounts;
    }

    private static void AddPowerToken(Dictionary<string, int> powerAmounts, string token, int amount)
    {
        string normalized = NormalizeToken(token);
        if (normalized.Length == 0)
        {
            return;
        }

        powerAmounts[normalized] = powerAmounts.GetValueOrDefault(normalized) + amount;
    }

    private static string NormalizeToken(string value)
    {
        char[] chars = value
            .Select(static character => char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_')
            .ToArray();
        return new string(chars);
    }

    private static string GetCardInstanceId(CardModel card)
    {
        return NetCombatCardDb.Instance.TryGetCardId(card, out uint cardId)
            ? $"combat_{cardId}"
            : card.Id.Entry.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }

    private static string GetTargetId(Creature target)
    {
        if (target.Player != null)
        {
            return $"player_{target.Player.NetId}";
        }

        return $"creature_{target.CombatId?.ToString() ?? target.Name.Replace(' ', '_')}";
    }

    private readonly record struct IncomingDamageProjection(
        int BlockableDamage,
        int UnblockableDamage,
        int ExpectedBlockAtEnemyTurn,
        int FinalDamageAfterBlock);

    private readonly record struct EstimatedDamageAction(
        int Damage,
        int EnergyCost,
        int StarCost,
        bool IsAllEnemies,
        string SourceKey);
}
