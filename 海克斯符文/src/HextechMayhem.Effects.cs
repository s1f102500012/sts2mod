using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
    private async Task RunGroupedPlayerDebuffBurst(Func<Task> action)
    {
        bool wasHandlingGroupedPlayerDebuffs = _handlingGroupedPlayerDebuffs;
        if (!wasHandlingGroupedPlayerDebuffs)
        {
            _handlingGroupedPlayerDebuffs = true;
            _groupedPlayerDebuffProcKeys.Clear();
        }

        try
        {
            await action();
        }
        finally
        {
            if (!wasHandlingGroupedPlayerDebuffs)
            {
                _groupedPlayerDebuffProcKeys.Clear();
                _handlingGroupedPlayerDebuffs = false;
            }
        }
    }

    private const decimal ProtectiveVeilInitialArtifactStacks = 1m;

    private const decimal RepulsorSlipperyStacks = 2m;

    private const decimal ShrinkEngineSlipperyStacks = 1m;

    private const decimal CourageOfColossusBlockPercent = 0.2m;

    private const decimal CantTouchThisSlipperyStacks = 1m;

    private async Task ApplyPersistentMonsterHexes(Creature creature)
    {
        if (HasActiveMonsterHex(MonsterHexKind.Goliath)
            && creature.CombatId != null
            && _goliathApplied.Add(creature.CombatId.Value))
        {
            int maxHpGain = (int)Math.Floor(creature.MaxHp * 0.35m);
            if (maxHpGain > 0)
            {
                await CreatureCmd.GainMaxHp(creature, maxHpGain);
            }

            UpdateEnemyScale(creature);
        }

        if (HasActiveMonsterHex(MonsterHexKind.AstralBody)
            && creature.CombatId != null
            && _astralBodyApplied.Add(creature.CombatId.Value))
        {
            int maxHpGain = (int)Math.Floor(creature.MaxHp * 0.3m);
            if (maxHpGain > 0)
            {
                await CreatureCmd.GainMaxHp(creature, maxHpGain);
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.MadScientist)
            && creature.CombatId != null
            && _madScientistApplied.Add(creature.CombatId.Value))
        {
            int maxHpLoss = Math.Max(1, (int)Math.Floor(creature.MaxHp * 0.2m));
            int newMaxHp = Math.Max(1, creature.MaxHp - maxHpLoss);
            if (newMaxHp < creature.MaxHp)
            {
                await CreatureCmd.SetMaxHp(creature, newMaxHp);
            }

            await PowerCmd.Apply<PersonalHivePower>(creature, 1m, creature, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.DrawYourSword)
            && TryMarkPersistentHexApplied(_drawYourSwordApplied, creature))
        {
            await PowerCmd.Apply<ImbalancedPower>(creature, 1m, creature, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.BigStrength)
            && TryMarkPersistentHexApplied(_bigStrengthApplied, creature))
        {
            await PowerCmd.Apply<StrengthPower>(creature, 2m, creature, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.ProtectiveVeil)
            && TryMarkPersistentHexApplied(_protectiveVeilApplied, creature))
        {
            await PowerCmd.Apply<ArtifactPower>(creature, ProtectiveVeilInitialArtifactStacks, creature, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.Thornmail)
            && TryMarkPersistentHexApplied(_thornmailApplied, creature))
        {
            await PowerCmd.Apply<ReflectPower>(creature, 5m, creature, null);
        }

        if (HasActiveMonsterHex(MonsterHexKind.SuperBrain)
            && TryMarkPersistentHexApplied(_superBrainApplied, creature))
        {
            int plating = (int)Math.Floor(creature.MaxHp * 0.04m);
            if (plating > 0)
            {
                await PowerCmd.Apply<PlatingPower>(creature, plating, creature, null);
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.GlassCannon))
        {
            int hpCap = Math.Max(1, (int)Math.Floor(creature.MaxHp * 0.7m));
            if (creature.CurrentHp > hpCap)
            {
                await CreatureCmd.SetCurrentHp(creature, hpCap);
            }
        }

        if (HasActiveMonsterHex(MonsterHexKind.UnmovableMountain)
            && TryMarkPersistentHexApplied(_unmovableMountainApplied, creature))
        {
            await PowerCmd.Apply<BarricadePower>(creature, 1m, creature, null);
        }

        await TryApplyServantMasterIllusion(creature, creature, null);
    }

    private async Task NormalizeEnemyPainfulStabsPowers(CombatState combatState)
    {
        if (!HasActiveMonsterHex(MonsterHexKind.GetExcited))
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies.ToList())
        {
            if (enemy.CombatState != combatState)
            {
                continue;
            }

            PainfulStabsPower? legacyPower = enemy.GetPower<PainfulStabsPower>();
            if (legacyPower != null && enemy.IsDead)
            {
                await PowerCmd.Remove(legacyPower);
            }

            RemoveRetainedDeadEnemyIfNeeded(combatState, enemy);
        }
    }

    private static void RemoveRetainedDeadEnemyIfNeeded(CombatState combatState, Creature enemy)
    {
        if (enemy.Side != CombatSide.Enemy
            || enemy.IsAlive
            || !combatState.Enemies.Contains(enemy)
            || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, enemy))
        {
            return;
        }

        var node = NCombatRoom.Instance?.GetCreatureNode(enemy);
        if (node != null)
        {
            NCombatRoom.Instance?.RemoveCreatureNode(node);
        }

        CombatManager.Instance.RemoveCreature(enemy);
        combatState.RemoveCreature(enemy);
        Log.Info($"[{ModInfo.Id}][Mayhem] Removed retained dead enemy after unsafe PainfulStabs cleanup: id={enemy.CombatId?.ToString() ?? "none"} model={enemy.ModelId.Entry}");
    }

    private async Task TryApplyServantMasterIllusion(Creature creature, Creature? applier, CardModel? cardSource)
    {
        if (_handlingServantMasterIllusion
            || !HasActiveMonsterHex(MonsterHexKind.ServantMaster)
            || creature.Side != CombatSide.Enemy
            || !creature.IsAlive
            || creature.CombatState?.RunState != RunState
            || !creature.HasPower<MinionPower>()
            || creature.HasPower<IllusionPower>())
        {
            return;
        }

        try
        {
            _handlingServantMasterIllusion = true;
            await PowerCmd.Apply<IllusionPower>(creature, 1m, applier ?? creature, cardSource);
        }
        finally
        {
            _handlingServantMasterIllusion = false;
        }
    }

    private static void DowngradePlayerCombatCards(CombatState combatState)
    {
        foreach (CardModel card in combatState.Players
            .SelectMany(static player => player.PlayerCombatState?.AllCards ?? Array.Empty<CardModel>())
            .Where(static card => card.IsUpgraded)
            .ToList())
        {
            CardCmd.Downgrade(card);
        }
    }

    private void UpdateEnemyScale(Creature creature)
    {
        float baseScale = HasActiveMonsterHex(MonsterHexKind.Goliath) ? 1.35f : 1f;
        int tankStacks = creature.CombatId == null ? 0 : _tankEngineStacks.GetValueOrDefault(creature.CombatId.Value, 0);
        int shrinkStacks = creature.CombatId == null ? 0 : _shrinkEngineStacks.GetValueOrDefault(creature.CombatId.Value, 0);
        float finalScale = Math.Max(0.2f, baseScale + tankStacks * 0.05f - shrinkStacks * 0.02f);
        NCombatRoom.Instance?.GetCreatureNode(creature)?.SetDefaultScaleTo(finalScale, 0f);
    }

    private static IReadOnlyList<Creature> GetAliveEnemies(CombatState combatState)
    {
        return combatState.Enemies.Where(static creature => creature.IsAlive).ToList();
    }

    private static IReadOnlyList<Creature> GetAlivePlayerSideCreatures(CombatState combatState)
    {
        return combatState.PlayerCreatures.Where(static creature => creature.IsAlive).ToList();
    }

    private static bool TryConsumeLimitedProc(Dictionary<uint, int> counts, Creature creature, int maxPerTurn)
    {
        if (creature.CombatId == null)
        {
            return false;
        }

        uint combatId = creature.CombatId.Value;
        int current = counts.GetValueOrDefault(combatId, 0);
        if (current >= maxPerTurn)
        {
            return false;
        }

        counts[combatId] = current + 1;
        return true;
    }

    private static bool TryGetMonsterDebuffTrigger(PowerModel power, decimal amount, Creature? applier, out Creature? target, out Creature? source)
    {
        target = power.Owner;
        source = applier;
        return amount > 0m
            && target?.Side == CombatSide.Player
            && source?.Side == CombatSide.Enemy
            && power.GetTypeForAmount(amount) == PowerType.Debuff
            && power is not ITemporaryPower;
    }

    private bool ShouldSuppressMonsterDebuffDuplicate(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        string powerTypeName = power.GetType().FullName ?? power.GetType().Name;
        if (_handlingGroupedPlayerDebuffs)
        {
            string groupedKey = $"{applier?.CombatId?.ToString() ?? "none"}:{powerTypeName}:{amount}";
            return !_groupedPlayerDebuffProcKeys.Add(groupedKey);
        }

        if (cardSource == null || applier?.CombatId == null)
        {
            return false;
        }

        string actionKey = $"{applier.CombatId.Value}:{RuntimeHelpers.GetHashCode(cardSource)}:{powerTypeName}:{amount}";
        return !_monsterDebuffActionProcKeysThisTurn.Add(actionKey);
    }

    private bool ShouldSuppressDuplicateEnemyThresholdTrigger(Creature target, DamageResult result, Creature? dealer, CardModel? cardSource)
    {
        string key = string.Join(":",
            target.CombatId?.ToString() ?? "none",
            target.CurrentHp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.UnblockedDamage.ToString(System.Globalization.CultureInfo.InvariantCulture),
            dealer?.CombatId?.ToString() ?? "none",
            cardSource != null ? RuntimeHelpers.GetHashCode(cardSource).ToString() : "none");
        bool suppress = key == _lastEnemyThresholdTriggerKey;
        _lastEnemyThresholdTriggerKey = key;
        return suppress;
    }

    private static bool TryGetMonsterSelfBuffTrigger(PowerModel power, decimal amount, Creature? applier, out Creature? source)
    {
        source = null;
        Creature? owner = power.Owner;
        if (amount <= 0m
            || owner?.Side != CombatSide.Enemy
            || power.GetTypeForAmount(amount) != PowerType.Buff
            || HextechMonsterInteractionPolicy.ShouldIgnoreMonsterSelfBuff(power)
            || power is ITemporaryPower
            || power is PlatingPower
            || power is BufferPower
            || (applier != null && applier != owner))
        {
            return false;
        }

        source = owner;
        return true;
    }

    private static bool TryMarkPersistentHexApplied(HashSet<uint> appliedSet, Creature creature)
    {
        return creature.CombatId != null && appliedSet.Add(creature.CombatId.Value);
    }

    private void TrackPlayerAttackCardPlayed(CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries
            || cardPlay.IsAutoPlay
            || cardPlay.Card.Type != CardType.Attack
            || cardPlay.Card.Owner?.Creature.Side != CombatSide.Player)
        {
            return;
        }

        ulong playerId = cardPlay.Card.Owner.NetId;
        _playerAttackCardsPlayedThisCombat[playerId] = _playerAttackCardsPlayedThisCombat.GetValueOrDefault(playerId, 0) + 1;
    }

    private int GetPlayerAttacksPlayedThisCombat(CardModel card)
    {
        return card.Owner == null ? 0 : _playerAttackCardsPlayedThisCombat.GetValueOrDefault(card.Owner.NetId, 0);
    }

    public decimal ModifyEnemyHealAmount(Creature creature, decimal amount)
    {
        if (creature.Side != CombatSide.Enemy)
        {
            return amount;
        }

        if (HasActiveMonsterHex(MonsterHexKind.Goliath))
        {
            amount *= 1.2m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.FirstAidKit))
        {
            amount *= 1.2m;
        }

        if (HasActiveMonsterHex(MonsterHexKind.GlassCannon))
        {
            int healCap = (int)Math.Floor(creature.MaxHp * 0.7m);
            amount = Math.Min(amount, Math.Max(0, healCap - creature.CurrentHp));
        }

        return amount;
    }
}
