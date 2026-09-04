using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class DeterministicCombatContext
{
    public required Player Actor { get; init; }

    public required IReadOnlyList<AiLegalActionOption> LegalActions { get; init; }

    public required Dictionary<string, ResolvedCardView> HandCardsByInstanceId { get; init; }

    public required Dictionary<string, DeterministicEnemyState> EnemiesById { get; init; }

    public required Dictionary<string, DeterministicPlayerState> PlayerStatesById { get; init; }

    public required IReadOnlyList<ResolvedCardView> KnownDrawPileTopCards { get; init; }

    public required Dictionary<string, int> ActorPowerAmounts { get; init; }

    public required HashSet<string> ActorRelicIds { get; init; }

    public required AiCharacterCombatConfig CombatConfig { get; init; }

    public required string RoomTypeName { get; init; }

    public required string EncounterId { get; init; }

    public bool IsEliteCombat { get; init; }

    public bool IsBossCombat { get; init; }

    public bool IsEliteOrBossCombat => IsEliteCombat || IsBossCombat;

    public bool IsPhantasmalGardenersCombat { get; init; }

    public bool IsKaiserCrabCombat { get; init; }

    public bool IsObscuraCombat { get; init; }

    public bool IsWaterfallSelfDestructDefenseWindow =>
        EnemiesById.Values.Any(static enemy => enemy.IsWaterfallGiant && enemy.HasAnyMoveToken("ABOUT_TO_BLOW"));

    public bool IsLagavulinMatriarchAsleep =>
        EnemiesById.Values.Any(static enemy => enemy.IsLagavulinMatriarchAsleep);

    public bool IsLagavulinMatriarchOpeningSetupWindow =>
        IsLagavulinMatriarchAsleep && CombatRoundNumber <= 2;

    public int CombatRoundNumber { get; init; }

    public bool HasBlockRetention =>
        ActorRelicIds.Contains("CALIPERS") ||
        ActorRelicIds.Contains("CALIPER") ||
        ActorPowerAmounts.ContainsKey("BARRICADE");

    public int CurrentHp => Actor.Creature.CurrentHp;

    public int CurrentBlock => Actor.Creature.Block;

    public int Energy => Actor.PlayerCombatState?.Energy ?? 0;

    public int Stars => Actor.PlayerCombatState?.Stars ?? 0;

    public int ExpectedBlockAtEnemyTurn { get; init; }

    public int UnblockableIncomingDamage { get; init; }

    public int? PredictedIncomingDamageAfterBlock { get; init; }

    public int IncomingDamageAfterBlock =>
        PredictedIncomingDamageAfterBlock ??
        Math.Max(0, IncomingDamage - Math.Max(CurrentBlock, ExpectedBlockAtEnemyTurn)) +
            Math.Max(0, UnblockableIncomingDamage);

    public int AlivePlayerCount => PlayerStatesById.Count;

    public int TeamCurrentHp => PlayerStatesById.Values.Sum(static player => Math.Max(0, player.CurrentHp));

    public int TeamIncomingDamageAfterBlock => PlayerStatesById.Values.Sum(static player => player.IncomingDamageAfterBlock);

    public int TeamGraveDangerCount => PlayerStatesById.Values.Count(static player => player.IsInGraveDanger);

    public bool AnyTeamMemberInGraveDanger => TeamGraveDangerCount > 0;

    public bool IsTeamInCrisis =>
        AlivePlayerCount <= 2 ||
        AnyTeamMemberInGraveDanger ||
        TeamIncomingDamageAfterBlock >= Math.Max(28, TeamCurrentHp / 2);

    public int EnemyCount => EnemiesById.Count;

    public int TotalEnemyHp => EnemiesById.Values.Sum(static enemy => Math.Max(0, enemy.CurrentHp + enemy.Block));

    public int FilledPotionSlots => Actor.Potions.Count();

    public int MaxPotionSlots => Actor.PotionSlots.Count;

    public bool PotionSlotsFull => MaxPotionSlots > 0 && FilledPotionSlots >= MaxPotionSlots;

    public bool FuturePotionDropAfterCombat { get; init; }

    public string FuturePotionDropDescription { get; init; } = string.Empty;

    public bool ShouldSpendPotionSlotForFutureDrop => PotionSlotsFull && FuturePotionDropAfterCombat;

    public int IncomingDamage { get; init; }

    public int SustainedAttackPressure =>
        EnemiesById.Values.Sum(static enemy => enemy.SustainedAttackPressure);

    public bool HasSustainedAttackPressure => SustainedAttackPressure > 0;

    public bool HasCatastrophicEnemyAction =>
        EnemiesById.Values.Any(static enemy => enemy.HasCatastrophicMove) ||
        IncomingDamageAfterBlock >= Math.Max(35, CurrentHp * 2 / 3) ||
        TeamIncomingDamageAfterBlock >= Math.Max(70, TeamCurrentHp * 2 / 3);

    public required DeterministicTeamCombatTactics TeamTactics { get; init; }
}

internal sealed class DeterministicTeamCombatTactics
{
    public static readonly DeterministicTeamCombatTactics Empty = new()
    {
        PrimaryTargetId = string.Empty,
        PrimaryTargetEffectiveHp = 0,
        EstimatedTeamDamageToPrimary = 0,
        EstimatedActorDamageToPrimary = 0,
        EstimatedTeamDamageTotal = 0,
        EstimatedActorDamageTotal = 0,
        KillableEnemyCount = 0,
        NonMinionEnemyCount = 0,
        CanKillAllNonMinionEnemies = false,
        IsTargetLock = false,
        TeamDamageByEnemyId = new Dictionary<string, int>(StringComparer.Ordinal),
        ActorDamageByEnemyId = new Dictionary<string, int>(StringComparer.Ordinal)
    };

    public required string PrimaryTargetId { get; init; }

    public required int PrimaryTargetEffectiveHp { get; init; }

    public required int EstimatedTeamDamageToPrimary { get; init; }

    public required int EstimatedActorDamageToPrimary { get; init; }

    public required int EstimatedTeamDamageTotal { get; init; }

    public required int EstimatedActorDamageTotal { get; init; }

    public required int KillableEnemyCount { get; init; }

    public required int NonMinionEnemyCount { get; init; }

    public required bool CanKillAllNonMinionEnemies { get; init; }

    public required bool IsTargetLock { get; init; }

    public required IReadOnlyDictionary<string, int> TeamDamageByEnemyId { get; init; }

    public required IReadOnlyDictionary<string, int> ActorDamageByEnemyId { get; init; }

    public bool HasFocusedKill =>
        !string.IsNullOrEmpty(PrimaryTargetId) &&
        PrimaryTargetEffectiveHp > 0 &&
        EstimatedTeamDamageToPrimary >= PrimaryTargetEffectiveHp;

    public bool HasPrimaryTarget =>
        !string.IsNullOrEmpty(PrimaryTargetId) &&
        PrimaryTargetEffectiveHp > 0;

    public bool ActorCanContributeToPrimary => EstimatedActorDamageToPrimary > 0;

    public string Describe()
    {
        if (!HasFocusedKill && !IsTargetLock)
        {
            return $"focusedKill=false killable={KillableEnemyCount} nonMinionLethal={CanKillAllNonMinionEnemies} teamDamage={EstimatedTeamDamageTotal}";
        }

        return $"focusedKill={HasFocusedKill} targetLock={IsTargetLock} target={PrimaryTargetId} hp={PrimaryTargetEffectiveHp} teamDamage={EstimatedTeamDamageToPrimary} actorDamage={EstimatedActorDamageToPrimary} killable={KillableEnemyCount} nonMinionLethal={CanKillAllNonMinionEnemies}";
    }
}

internal sealed class DeterministicEnemyState
{
    public required string Id { get; init; }

    public required Creature Creature { get; init; }

    public required Dictionary<string, int> PowerAmounts { get; init; }

    public int CurrentHp => Creature.CurrentHp;

    public int Block => Creature.Block;

    public int IncomingDamage { get; init; }

    public string NextMoveId { get; init; } = string.Empty;

    public string MonsterId { get; init; } = string.Empty;

    public bool IsAttacking => IncomingDamage > 0;

    public bool HasCatastrophicMove => HasMoveToken("ABOUT_TO_BLOW", "EXPLODE");

    public bool HasSummonMove => HasMoveToken("SUMMON", "CALL_FOR_BACKUP", "CALL_BACKUP", "LAY_EGG", "LAY_EGGS", "SPAWN");

    public bool IsLikelySummonedAdd =>
        HasMonsterToken("TOUGH_EGG", "EGG", "MINION", "SUMMONED", "HATCHLING") ||
        HasNameToken("TOUGH EGG", "EGG", "MINION", "SUMMONED", "HATCHLING");

    public bool IsActiveSummoner =>
        HasSummonMove ||
        HasMonsterToken("OVICOPTER", "SUMMONER", "SPAWNER", "BROOD") ||
        HasNameToken("OVICOPTER", "SUMMONER", "SPAWNER", "BROOD");

    public bool HasVulnerable => HasPowerToken("VULNERABLE");

    public bool HasWeak => HasPowerToken("WEAK");

    public bool HasArtifact => HasPowerToken("ARTIFACT");

    public bool HasBuffer => HasPowerToken("BUFFER");

    public bool HasIntangible => HasPowerToken("INTANGIBLE", "INCORPOREAL", "WRAITH");

    public bool IsKaiserCrabPart =>
        HasMonsterToken("KAISER", "CRAB", "CRUSHER", "ROCKET") ||
        HasMoveToken("TARGETING_RETICLE", "PRECISION_BEAM", "CHARGE_UP", "RECHARGE", "ENLARGING_STRIKE", "GUARDED_STRIKE");

    public bool IsWaterfallGiant =>
        HasMonsterToken("WATERFALL") ||
        HasNameToken("WATERFALL", "瀑布");

    public bool IsObscuraBody =>
        HasMonsterToken("THE_OBSCURA") ||
        HasNameToken("胧光怪", "朧光怪", "胧光", "朧光", "OBSCURA");

    public bool IsLagavulinMatriarch =>
        HasMonsterToken("LAGAVULIN_MATRIARCH", "LAGAVULINMATRIARCH") ||
        HasNameToken("乐加维林族母", "樂加維林族母", "LAGAVULIN MATRIARCH", "LAGAVULIN");

    public bool IsLagavulinMatriarchAsleep =>
        IsLagavulinMatriarch &&
        (HasMoveToken("SLEEP", "SLEEP_INTENT", "SLEEPINTENT", "INITIAL_SLEEP", "INITIALSLEEP", "POOR_SLEEP") ||
         HasPowerToken("ASLEEP", "SLEEP", "DORMANT"));

    public bool IsCorpseSlug =>
        HasMonsterToken("CORPSE_SLUG", "CORPSESLUG") ||
        HasNameToken("噬尸蛞蝓", "噬屍蛞蝓", "CORPSE SLUG", "CORPSESLUG");

    public bool IsCorpseSlugDebuffIntent =>
        IsCorpseSlug &&
        HasMoveToken("GOOP", "DEBUFF", "CARD_DEBUFF", "CARDDEBUFF", "SLIME", "NEGATIVE", "SPORE", "FRAIL", "WEAK", "VULNERABLE");

    public bool IsGremlinMerc =>
        HasMonsterToken("GREMLIN_MERC", "GREMLINMERC") ||
        HasNameToken("GREMLIN MERC");

    public bool IsFatGremlin =>
        HasMonsterToken("FAT_GREMLIN", "FATGREMLIN") ||
        HasNameToken("FAT GREMLIN", "胖地精", "肥地精");

    public bool IsSneakyGremlin =>
        HasMonsterToken("SNEAKY_GREMLIN", "SNEAKYGREMLIN") ||
        HasNameToken("SNEAKY GREMLIN", "狡猾地精", "潜行地精");

    public bool IsKaiserCrabTurnLeverMove => HasMoveToken("TARGETING_RETICLE", "CHARGE_UP", "RECHARGE", "ADAPT");

    public bool IsKaiserCrabHeavyAttackMove => HasMoveToken("THRASH", "PRECISION_BEAM", "LASER", "ENLARGING_STRIKE", "GUARDED_STRIKE");

    public int PunishingAttackAmount => GetPowerAmountContaining("THORN", "SPIKE", "SHARP_HIDE", "SHARP");

    public bool PunishesAttacks => PunishingAttackAmount > 0;

    public int FutureAttackGrowthPerTurn => GetPowerAmountContaining(
        "RITUAL",
        "INCANT",
        "GROW",
        "GROWTH",
        "SCALING",
        "STRENGTH_UP");

    public int CurrentStrengthAmount => GetPowerAmountContaining("STRENGTH");

    public int SustainedAttackPressure
    {
        get
        {
            int growthPerTurn = FutureAttackGrowthPerTurn;
            int movePressure = EstimateMoveBasedPressure();
            if (growthPerTurn <= 0 && movePressure <= 0)
            {
                return 0;
            }

            int pressure = movePressure + growthPerTurn * 14;
            pressure += Math.Min(45, CurrentStrengthAmount * 3);
            pressure += Math.Min(70, IncomingDamage * 2);

            if (CurrentHp + Block <= 40)
            {
                pressure += 18;
            }

            return Math.Min(320, pressure);
        }
    }

    public int EstimateAttackPunishPenalty(int hits)
    {
        if (!PunishesAttacks)
        {
            return 0;
        }

        return Math.Max(18, PunishingAttackAmount * Math.Max(hits, 1) * 8);
    }

    public bool HasAnyMoveToken(params string[] tokens)
    {
        return HasMoveToken(tokens);
    }

    private bool HasPowerToken(params string[] tokens)
    {
        return PowerAmounts.Keys.Any(powerId => tokens.Any(token => powerId.Contains(token, StringComparison.Ordinal)));
    }

    private bool HasMoveToken(params string[] tokens)
    {
        return tokens.Any(token => NextMoveId.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private int EstimateMoveBasedPressure()
    {
        if (HasCatastrophicMove)
        {
            return 260;
        }

        if (HasMoveToken("PRESSURE_UP", "RITUAL", "ENRAGE", "GROW", "SCALING"))
        {
            return 120;
        }

        if (HasSummonMove)
        {
            return 105;
        }

        if (IsKaiserCrabHeavyAttackMove)
        {
            return 85;
        }

        if (HasMoveToken("SCREAM", "GAZE", "BECKON", "DE_GAS", "FADE"))
        {
            return 70;
        }

        return 0;
    }

    private bool HasMonsterToken(params string[] tokens)
    {
        return tokens.Any(token => MonsterId.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasNameToken(params string[] tokens)
    {
        string name = Creature.Name ?? string.Empty;
        return tokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private int GetPowerAmountContaining(params string[] tokens)
    {
        int amount = 0;
        foreach (KeyValuePair<string, int> power in PowerAmounts)
        {
            if (tokens.Any(token => power.Key.Contains(token, StringComparison.Ordinal)))
            {
                amount = Math.Max(amount, Math.Max(power.Value, 1));
            }
        }

        return amount;
    }
}

internal sealed class DeterministicPlayerState
{
    public required string Id { get; init; }

    public required Creature Creature { get; init; }

    public required bool IsActor { get; init; }

    public int CurrentHp => Creature.CurrentHp;

    public int MaxHp => Creature.MaxHp;

    public int Block => Creature.Block;

    public int IncomingDamage { get; init; }

    public int ExpectedBlockAtEnemyTurn { get; init; }

    public int UnblockableIncomingDamage { get; init; }

    public int? PredictedIncomingDamageAfterBlock { get; init; }

    public int IncomingDamageAfterBlock =>
        PredictedIncomingDamageAfterBlock ??
        Math.Max(0, IncomingDamage - Math.Max(Block, ExpectedBlockAtEnemyTurn)) +
            Math.Max(0, UnblockableIncomingDamage);

    public bool IsInGraveDanger =>
        IncomingDamageAfterBlock >= CurrentHp ||
        IncomingDamageAfterBlock >= Math.Max(10, CurrentHp / 3);

    public int MissingHp => Math.Max(0, MaxHp - CurrentHp);
}
