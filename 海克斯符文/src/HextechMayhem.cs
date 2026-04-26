using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes;

internal enum HextechRarityTier
{
    Silver = 0,
    Gold = 1,
    Prismatic = 2
}

internal enum MonsterHexKind
{
    Slap = 0,
    EscapePlan = 1,
    HeavyHitter = 2,
    BigStrength = 3,
    Tormentor = 4,
    ProtectiveVeil = 5,
    Repulsor = 6,
    Thornmail = 7,
    Sturdy = 8,
    DawnbringersResolve = 9,
    ShrinkRay = 10,
    Firebrand = 11,
    SuperBrain = 12,
    AstralBody = 13,
    Nightstalking = 14,
    CourageOfColossus = 15,
    GlassCannon = 16,
    Goliath = 17,
    Queen = 18,
    HandOfBaron = 19,
    CantTouchThis = 20,
    MasterOfDuality = 21,
    Goldrend = 22,
    TankEngine = 23,
    GetExcited = 24,
    ShrinkEngine = 25,
    FeelTheBurn = 26,
    LightEmUp = 27,
    MountainSoul = 28,
    TwiceThrice = 29,
    Loop = 30,
    ServantMaster = 31,
    BackToBasics = 32,
    DrawYourSword = 33,
    MadScientist = 34,
    FirstAidKit = 35,
    SpeedDemon = 36,
    DivineIntervention = 37,
    Sonata = 38,
    FeyMagic = 39,
    FinalForm = 40,
    UnmovableMountain = 41,
    MikaelsBlessing = 42,
    DevilsDance = 43
}

internal sealed partial class HextechMayhemModifier : ModifierModel
{
    public override async Task AfterActEntered()
    {
        int actIndex = RunState.CurrentActIndex;
        if (!IsActResolved(actIndex) && TryRecoverResolvedActsFromPlayerRelics(nameof(AfterActEntered)))
        {
            HextechEnemyUi.Refresh(this);
        }

        if (actIndex <= 0 || actIndex > 2 || IsActResolved(actIndex))
        {
            return;
        }

        Log.Info($"[{ModInfo.Id}][Mayhem] AfterActEntered: resolving act selection before first room actIndex={actIndex}");
        await HextechRuneSelectionCoordinator.HandleActSelection(RunState, this);
    }

    public override async Task BeforeRoomEntered(AbstractRoom room)
    {
        int actIndex = RunState.CurrentActIndex;
        if (!IsActResolved(actIndex) && TryRecoverResolvedActsFromPlayerRelics(nameof(BeforeRoomEntered)))
        {
            HextechEnemyUi.Refresh(this);
        }

        if (actIndex < 0 || actIndex > 2 || IsActResolved(actIndex) || room is EventRoom or MapRoom)
        {
            return;
        }

        if (actIndex == 0)
        {
            Log.Warn($"[{ModInfo.Id}][Mayhem] BeforeRoomEntered: skipping unsafe act0 selection before room={room.GetType().Name}; waiting for post-Neow or map path");
            return;
        }

        Log.Info($"[{ModInfo.Id}][Mayhem] BeforeRoomEntered: resolving pending act selection before room={room.GetType().Name} actIndex={actIndex}");
        await HextechRuneSelectionCoordinator.HandleActSelection(RunState, this);
    }

    public override async Task BeforeCombatStart()
    {
        HextechGoldrendSync.ResetCombat();
        ResetCombatTracking();
        HextechEnemyUi.Refresh(this);
        await ApplyToCurrentEnemiesIfNeeded();

        if (HasActiveMonsterHex(MonsterHexKind.Queen)
            && RunState.CurrentRoom is CombatRoom combatRoom)
        {
            IReadOnlyList<Creature> players = GetAlivePlayerSideCreatures(combatRoom.CombatState);
            if (players.Count > 0)
            {
                await RunGroupedPlayerDebuffBurst(async () =>
                {
                    await PowerCmd.Apply<ChainsOfBindingPower>(players, 1m, null, null);
                });
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.HandOfBaron)
            && RunState.CurrentRoom is CombatRoom combatRoomForBaron)
        {
            IReadOnlyList<Creature> players = GetAlivePlayerSideCreatures(combatRoomForBaron.CombatState);
            if (players.Count > 0)
            {
                await RunGroupedPlayerDebuffBurst(async () =>
                {
                    await PowerCmd.Apply<ShrinkPower>(players, 99m, null, null);
                });
            }
        }

        _enemyProtectiveVeilTurnCounter = 0;
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await HextechGoldrendSync.ApplyPendingCombatGoldLosses(RunState);
        ResetCombatTracking();
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        return HextechForgeGrantHelper.TryAddRandomForgeRewardAfterVictory(RunState, room);
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsAlive)
        {
            return;
        }

        await ApplyPersistentMonsterHexes(creature);
        await TryApplyServantMasterIllusion(creature, creature, null);
        HextechEnemyUi.Refresh(this);
    }

    public async Task ApplyToCurrentEnemiesIfNeeded()
    {
        if (RunState.CurrentRoom is not CombatRoom combatRoom)
        {
            return;
        }

        foreach (Creature enemy in combatRoom.CombatState.Enemies.Where(static creature => creature.IsAlive))
        {
            await ApplyPersistentMonsterHexes(enemy);
        }

        HextechEnemyUi.Refresh(this);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
    {
        await NormalizeEnemyPainfulStabsPowers(combatState);

        IReadOnlyList<Creature> players = GetAlivePlayerSideCreatures(combatState);

        if (side == CombatSide.Player)
        {
            await ApplyToCurrentEnemiesIfNeeded();

            if (_escapePlanPending.Count > 0)
            {
                foreach (uint combatId in _escapePlanPending.ToList())
                {
                    Creature? creature = combatState.GetCreature(combatId);
                    _escapePlanPending.Remove(combatId);
                    if (creature == null || !creature.IsAlive)
                    {
                        continue;
                    }

                    int blockAmount = (int)Math.Floor(creature.MaxHp * 0.6m);
                    if (blockAmount > 0)
                    {
                        await CreatureCmd.GainBlock(creature, blockAmount, ValueProp.Unpowered, null);
                    }

                    await PowerCmd.Apply<ShrinkPower>(creature, 1m, creature, null);
                }
            }

            if (_speedDemonPending.Count > 0)
            {
                foreach (uint combatId in _speedDemonPending.ToList())
                {
                    Creature? creature = combatState.GetCreature(combatId);
                    _speedDemonPending.Remove(combatId);
                    if (creature == null || !creature.IsAlive)
                    {
                        continue;
                    }

                    int blockAmount = Math.Max(1, (int)Math.Floor(creature.MaxHp * 0.1m));
                    await CreatureCmd.GainBlock(creature, blockAmount, ValueProp.Unpowered, null);
                }
            }

            if (_feyMagicPendingNoDrawPlayers.Count > 0)
            {
                foreach (KeyValuePair<uint, uint> pending in _feyMagicPendingNoDrawPlayers.ToList())
                {
                    uint combatId = pending.Key;
                    Creature? creature = combatState.GetCreature(combatId);
                    Creature? source = combatState.GetCreature(pending.Value);
                    _feyMagicPendingNoDrawPlayers.Remove(combatId);
                    if (creature == null || !creature.IsAlive || creature.Side != CombatSide.Player)
                    {
                        continue;
                    }

                    await PowerCmd.Apply<NoDrawPower>(creature, 1m, source, null);
                }
            }

            if (_repulsorPending.Count > 0)
            {
                foreach (uint combatId in _repulsorPending.ToList())
                {
                    Creature? creature = combatState.GetCreature(combatId);
                    _repulsorPending.Remove(combatId);
                    if (creature == null || !creature.IsAlive)
                    {
                        continue;
                    }

                    await PowerCmd.Apply<SlipperyPower>(creature, RepulsorSlipperyStacks, creature, null);
                }
            }

            if (HasActiveMonsterHex(MonsterHexKind.MountainSoul))
            {
                foreach (Creature enemy in GetAliveEnemies(combatState))
                {
                    if (enemy.CombatId == null)
                    {
                        continue;
                    }

                    uint combatId = enemy.CombatId.Value;
                    if (_mountainSoulHasPreviousTurn.Contains(combatId)
                        && !_mountainSoulDamagedSinceLastTurn.Contains(combatId))
                    {
                        int block = Math.Max(1, (int)Math.Floor(enemy.MaxHp * 0.1m));
                        await CreatureCmd.GainBlock(enemy, block, ValueProp.Unpowered, null);
                    }

                    _mountainSoulHasPreviousTurn.Add(combatId);
                    _mountainSoulDamagedSinceLastTurn.Remove(combatId);
                }
            }

            return;
        }

        if (side != CombatSide.Enemy)
        {
            return;
        }

        _enemyProtectiveVeilTurnCounter++;
        _slapProcsThisTurn.Clear();
        _tormentorProcsThisTurn.Clear();
        _courageProcsThisTurn.Clear();
        _monsterDebuffActionProcKeysThisTurn.Clear();

        IReadOnlyList<Creature> enemies = GetAliveEnemies(combatState);

        if (HasActiveMonsterHex(MonsterHexKind.TankEngine))
        {
            foreach (Creature enemy in enemies)
            {
                int hpGain = Math.Min(5, Math.Max(1, (int)Math.Floor(enemy.MaxHp * 0.05m)));
                await CreatureCmd.GainMaxHp(enemy, hpGain);
                if (enemy.CombatId != null)
                {
                    uint combatId = enemy.CombatId.Value;
                    _tankEngineStacks[combatId] = _tankEngineStacks.GetValueOrDefault(combatId, 0) + 1;
                    UpdateEnemyScale(enemy);
                }
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.ShrinkEngine))
        {
            foreach (Creature enemy in enemies)
            {
                if (enemy.GetPowerAmount<SlipperyPower>() <= 0m)
                {
                    await PowerCmd.Apply<SlipperyPower>(enemy, ShrinkEngineSlipperyStacks, enemy, null);
                }

                if (enemy.CombatId != null)
                {
                    uint combatId = enemy.CombatId.Value;
                    _shrinkEngineStacks[combatId] = _shrinkEngineStacks.GetValueOrDefault(combatId, 0) + 1;
                    UpdateEnemyScale(enemy);
                }
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.Sturdy))
        {
            foreach (Creature enemy in enemies)
            {
                decimal percent = enemy.CurrentHp * 2 < enemy.MaxHp ? 0.04m : 0.02m;
                int heal = Math.Min(10, Math.Max(1, (int)Math.Floor(enemy.MaxHp * percent)));
                if (heal > 0)
                {
                    await CreatureCmd.Heal(enemy, heal);
                }
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.UnmovableMountain))
        {
            foreach (Creature enemy in enemies)
            {
                if (enemy.Block <= 0)
                {
                    int block = Math.Max(1, (int)Math.Floor(enemy.MaxHp * 0.08m));
                    await CreatureCmd.GainBlock(enemy, block, ValueProp.Unpowered, null);
                }
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.DivineIntervention)
            && combatState.RoundNumber > 1
            && combatState.RoundNumber % 4 == 0)
        {
            await PowerCmd.Apply<IntangiblePower>(enemies, 1m, null, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.Sonata))
        {
            if (combatState.RoundNumber % 2 == 1)
            {
                foreach (Creature enemy in enemies)
                {
                    int block = Math.Max(1, (int)Math.Floor(enemy.MaxHp * 0.1m));
                    await CreatureCmd.GainBlock(enemy, block, ValueProp.Unpowered, null);
                }
            }
            else
            {
                foreach (Creature enemy in enemies)
                {
                    int heal = Math.Max(1, (int)Math.Floor(enemy.MaxHp * 0.05m));
                    await CreatureCmd.Heal(enemy, heal);
                }
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.ProtectiveVeil)
            && _enemyProtectiveVeilTurnCounter % 2 == 0)
        {
            foreach (Creature enemy in enemies)
            {
                await PowerCmd.Apply<ArtifactPower>(enemy, 1m, enemy, null);
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.HandOfBaron) && players.Count > 0)
        {
            await RunGroupedPlayerDebuffBurst(async () =>
            {
                await PowerCmd.Apply<ShrinkPower>(players, 2m, null, null);
            });
        }

        if (HasActiveMonsterHex(MonsterHexKind.Queen)
            && combatState.RoundNumber > 1
            && combatState.RoundNumber % 3 == 0)
        {
            IReadOnlyList<Creature> queenTargets = players
                .Where(player => player.GetPowerAmount<ChainsOfBindingPower>() <= 3m)
                .ToList();
            if (queenTargets.Count > 0)
            {
                await RunGroupedPlayerDebuffBurst(async () =>
                {
                    await PowerCmd.Apply<ChainsOfBindingPower>(queenTargets, 1m, null, null);
                });
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.FeelTheBurn) && _feelTheBurnPending.Count > 0 && players.Count > 0)
        {
            foreach (uint combatId in _feelTheBurnPending.ToList())
            {
                Creature? creature = combatState.GetCreature(combatId);
                _feelTheBurnPending.Remove(combatId);
                if (creature == null || !creature.IsAlive)
                {
                    continue;
                }

                await RunGroupedPlayerDebuffBurst(async () =>
                {
                    await PowerCmd.Apply<WeakPower>(players, 1m, creature, null);
                    await PowerCmd.Apply<VulnerablePower>(players, 1m, creature, null);
                    await PowerCmd.Apply<HextechBurnPower>(players, 5m, creature, null);
                });
            }
        }

    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer?.Side != CombatSide.Enemy || dealer.CombatState?.RunState != RunState)
        {
            return 1m;
        }

        decimal multiplier = 1m;
        if (HasActiveMonsterHex(MonsterHexKind.HeavyHitter))
        {
            multiplier *= 1m + Math.Min(30m, Math.Floor(dealer.MaxHp / 10m)) / 100m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.GlassCannon))
        {
            multiplier *= 1.4m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.AstralBody))
        {
            multiplier *= 0.9m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.Goliath))
        {
            multiplier *= 1.2m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.DrawYourSword))
        {
            multiplier *= 1.4m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.HandOfBaron))
        {
            multiplier *= 1.2m;
        }

        return multiplier;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target.Side != CombatSide.Enemy || target.CombatState?.RunState != RunState)
        {
            return 1m;
        }

        decimal multiplier = 1m;
        if (HasActiveMonsterHex(MonsterHexKind.Goliath))
        {
            multiplier *= 1.2m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.FirstAidKit))
        {
            multiplier *= 1.2m;
        }

        return multiplier;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (!HasActiveMonsterHex(MonsterHexKind.Loop)
            || player.Creature.CombatState?.RunState != RunState)
        {
            return count;
        }

        return Math.Max(0m, count - 1m);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner?.Creature.Side != CombatSide.Player
            || card.Type != CardType.Attack
            || card.Pile?.Type != PileType.Hand
            || card.EnergyCost.CostsX
            || originalCost <= 0m
            || card.Owner.Creature.CombatState?.RunState != RunState)
        {
            return false;
        }

        int nextAttackIndex = GetPlayerAttacksPlayedThisCombat(card) + 1;
        decimal multiplier = 1m;
        if (HasActiveMonsterHex(MonsterHexKind.LightEmUp) && nextAttackIndex % 4 == 0)
        {
            multiplier *= 2m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.TwiceThrice) && nextAttackIndex % 3 == 0)
        {
            multiplier *= 2m;
        }

        if (multiplier == 1m)
        {
            return false;
        }

        modifiedCost = originalCost * multiplier;
        return true;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target.Side != CombatSide.Enemy || result.UnblockedDamage <= 0 || target.CombatId == null)
        {
            return;
        }

        uint combatId = target.CombatId.Value;
        if (HasActiveMonsterHex(MonsterHexKind.MountainSoul))
        {
            _mountainSoulDamagedSinceLastTurn.Add(combatId);
        }

        if (ShouldSuppressDuplicateEnemyThresholdTrigger(target, result, dealer, cardSource))
        {
            return;
        }

        decimal threshold = target.MaxHp * 0.5m;
        bool isBelowThresholdAfterDamage = target.CurrentHp < threshold;
        if (HasActiveMonsterHex(MonsterHexKind.EscapePlan)
            && !_escapePlanTriggered.Contains(combatId)
            && isBelowThresholdAfterDamage)
        {
            _escapePlanTriggered.Add(combatId);
            _escapePlanPending.Add(combatId);
        }

        if (HasActiveMonsterHex(MonsterHexKind.Repulsor)
            && !_repulsorTriggered.Contains(combatId)
            && isBelowThresholdAfterDamage)
        {
            _repulsorTriggered.Add(combatId);
            _repulsorPending.Add(combatId);
        }

        if (HasActiveMonsterHex(MonsterHexKind.DawnbringersResolve)
            && !_dawnTriggered.Contains(combatId)
            && isBelowThresholdAfterDamage)
        {
            _dawnTriggered.Add(combatId);
            int heal = Math.Max(1, (int)Math.Floor(target.MaxHp * 0.5m));
            await CreatureCmd.Heal(target, heal);
        }

        if (HasActiveMonsterHex(MonsterHexKind.FeelTheBurn)
            && isBelowThresholdAfterDamage
            && _feelTheBurnTriggered.Add(combatId))
        {
            _feelTheBurnPending.Add(combatId);
        }

        if (HasActiveMonsterHex(MonsterHexKind.MikaelsBlessing)
            && isBelowThresholdAfterDamage
            && _mikaelsBlessingTriggers.GetValueOrDefault(combatId, 0) < 3)
        {
            _mikaelsBlessingTriggers[combatId] = _mikaelsBlessingTriggers.GetValueOrDefault(combatId, 0) + 1;
            int heal = Math.Max(1, (int)Math.Floor(target.MaxHp * 0.3m));
            await CreatureCmd.Heal(target, heal);

            List<PowerModel> negativePowers = target.Powers
                .Where(static power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
                .ToList();
            foreach (PowerModel power in negativePowers)
            {
                await PowerCmd.Remove(power);
            }
        }
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer?.Side != CombatSide.Enemy || dealer.CombatState?.RunState != RunState || !target.IsAlive)
        {
            return;
        }

        if (HasActiveMonsterHex(MonsterHexKind.ShrinkRay) && result.UnblockedDamage > 0 && target.Side == CombatSide.Player)
        {
            await PowerCmd.Apply<ShrinkPower>(target, 1m, dealer, cardSource);
        }

        if (HasActiveMonsterHex(MonsterHexKind.Firebrand)
            && result.UnblockedDamage > 0
            && target.Side == CombatSide.Player
            && !HextechBurnPower.IsResolvingDamage)
        {
            await PowerCmd.Apply<HextechBurnPower>(target, 2m, dealer, cardSource);
        }

        if (HasActiveMonsterHex(MonsterHexKind.Goldrend)
            && result.UnblockedDamage > 0
            && target.Player != null)
        {
            await HextechGoldrendSync.HandleEnemyGoldrendHit(target.Player);
        }

        if (result.UnblockedDamage <= 0 || target.Side != CombatSide.Player)
        {
            return;
        }

        if (HasActiveMonsterHex(MonsterHexKind.DevilsDance) && dealer.IsAlive)
        {
            int heal = Math.Max(1, (int)Math.Floor(dealer.MaxHp * 0.1m));
            await CreatureCmd.Heal(dealer, heal);
        }

        if (HasActiveMonsterHex(MonsterHexKind.SpeedDemon)
            && dealer.IsAlive
            && dealer.CombatId != null)
        {
            _speedDemonPending.Add(dealer.CombatId.Value);
        }

        if (HasActiveMonsterHex(MonsterHexKind.CantTouchThis) && dealer.IsAlive)
        {
            await PowerCmd.Apply<SlipperyPower>(dealer, CantTouchThisSlipperyStacks, dealer, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.FeyMagic)
            && target.CombatId != null
            && dealer.CombatId != null
            && !_feyMagicPendingNoDrawPlayers.ContainsKey(target.CombatId.Value))
        {
            _feyMagicPendingNoDrawPlayers[target.CombatId.Value] = dealer.CombatId.Value;
        }

        if (HasActiveMonsterHex(MonsterHexKind.FinalForm) && dealer.IsAlive)
        {
            int block = Math.Max(1, (int)Math.Floor(dealer.MaxHp * 0.2m));
            await CreatureCmd.GainBlock(dealer, block, ValueProp.Unpowered, null);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        TrackPlayerAttackCardPlayed(cardPlay);

        if (!HasActiveMonsterHex(MonsterHexKind.MasterOfDuality)
            || cardPlay.Card.Owner?.Creature.Side != CombatSide.Player)
        {
            return;
        }

        Creature playerCreature = cardPlay.Card.Owner.Creature;
        if (!playerCreature.IsAlive)
        {
            return;
        }

        if (cardPlay.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Skill)
        {
            await PowerCmd.Apply<HextechTemporaryStrengthLossPower>(playerCreature, 1m, playerCreature, cardPlay.Card);
        }
        else if (cardPlay.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
        {
            await PowerCmd.Apply<HextechTemporaryDexterityLossPower>(playerCreature, 1m, playerCreature, cardPlay.Card);
        }
    }

    public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is MinionPower && amount > 0m)
        {
            await TryApplyServantMasterIllusion(power.Owner, applier, cardSource);
        }

        bool hasMonsterDebuffTrigger = TryGetMonsterDebuffTrigger(power, amount, applier, out Creature? target, out Creature? source);
        bool suppressMonsterDebuffDuplicate = hasMonsterDebuffTrigger && ShouldSuppressMonsterDebuffDuplicate(power, amount, source, cardSource);
        if (hasMonsterDebuffTrigger && !suppressMonsterDebuffDuplicate)
        {
            if (HasActiveMonsterHex(MonsterHexKind.Slap)
                && TryConsumeLimitedProc(_slapProcsThisTurn, source!, 3))
            {
                await PowerCmd.Apply<StrengthPower>(source!, 1m, source, null);
            }

            if (HasActiveMonsterHex(MonsterHexKind.Tormentor)
                && !_handlingMonsterTormentorBurn
                && TryConsumeLimitedProc(_tormentorProcsThisTurn, source!, 5))
            {
                try
                {
                    _handlingMonsterTormentorBurn = true;
                    await PowerCmd.Apply<HextechBurnPower>(target!, 2m, source, null);
                }
                finally
                {
                    _handlingMonsterTormentorBurn = false;
                }
            }
        }

        Creature? courageSource = null;
        bool hasCourageTrigger = false;
        if (hasMonsterDebuffTrigger && !suppressMonsterDebuffDuplicate)
        {
            courageSource = source;
            hasCourageTrigger = courageSource != null;
        }
        else if (TryGetMonsterSelfBuffTrigger(power, amount, applier, out Creature? buffSource))
        {
            courageSource = buffSource;
            hasCourageTrigger = true;
        }

        if (HasActiveMonsterHex(MonsterHexKind.CourageOfColossus)
            && hasCourageTrigger
            && TryConsumeLimitedProc(_courageProcsThisTurn, courageSource!, 1))
        {
            int block = Math.Max(1, (int)Math.Floor(courageSource!.MaxHp * CourageOfColossusBlockPercent));
            await CreatureCmd.GainBlock(courageSource, block, ValueProp.Unpowered, null);
        }

    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (!HasActiveMonsterHex(MonsterHexKind.GetExcited)
            || creature.Side != CombatSide.Enemy
            || creature.CombatState?.RunState != RunState)
        {
            return;
        }

        PainfulStabsPower? painfulStabs = creature.GetPower<PainfulStabsPower>();
        if (painfulStabs != null)
        {
            await PowerCmd.Remove(painfulStabs);
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented
            || target.Side != CombatSide.Enemy
            || !HextechMonsterInteractionPolicy.IsTrueCombatDeath(target, out CombatState? combatState))
        {
            return;
        }

        if (HasActiveMonsterHex(MonsterHexKind.Nightstalking))
        {
            IReadOnlyList<Creature> enemies = GetAliveEnemies(combatState)
                .Where(enemy => enemy != target)
                .ToList();
            if (enemies.Count > 0)
            {
                await PowerCmd.Apply<StrengthPower>(enemies, 1m, null, null);
                await PowerCmd.Apply<PaperCutsPower>(enemies, 1m, null, null);
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.GetExcited))
        {
            IReadOnlyList<Creature> enemies = GetAliveEnemies(combatState)
                .Where(enemy => enemy != target)
                .ToList();
            if (enemies.Count > 0)
            {
                await PowerCmd.Apply<StrengthPower>(enemies, 1m, null, null);
                await PowerCmd.Apply<PainfulStabsPower>(enemies, 1m, null, null);
            }
        }
    }
}
