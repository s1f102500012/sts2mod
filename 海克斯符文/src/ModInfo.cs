using System.Globalization;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes;

internal static class ModInfo
{
    public readonly record struct RuneSeriesGroup(string LocalizationKey, IReadOnlyList<RelicModel> Relics);

    public const string Id = "HextechRunes";

    public const string DisplayName = "海克斯符文";

    public const string TargetGameVersion = "0.103.2";

    public const string HextechSubcategoryKey = "HEXTECH_RUNES_SUBCATEGORY";

    private static readonly IReadOnlyList<Type> SilverRuneTypes =
    [
        typeof(SlapRune),
        typeof(DexterityToStrengthRune),
        typeof(StrengthToDexterityRune),
        typeof(DexterityStrengthToFocusRune),
        typeof(WizardlyThinkingRune),
        typeof(NimbleRune),
        typeof(EscapePlanRune),
        typeof(BadTasteRune),
        typeof(FirstAidKitRune),
        typeof(SpeedDemonRune),
        typeof(HeavyHitterRune),
        typeof(BigStrengthRune),
        typeof(TormentorRune),
        typeof(AdamantRune),
        typeof(MountainSoulRune),
        typeof(HomeguardRune),
        typeof(SwiftAndSafeRune),
        typeof(SacrificeRune),
        typeof(ProtectiveVeilRune),
        typeof(RepulsorRune),
        typeof(LightEmUpRune),
        typeof(UltimateUnstoppableRune),
        typeof(ThornmailRune),
        typeof(ZealotRune),
        typeof(TransmuteGoldRune)
    ];

    private static readonly IReadOnlyList<Type> GoldRuneTypes =
    [
        typeof(JudicatorRune),
        typeof(TranscendentEvilRune),
        typeof(TankEngineRune),
        typeof(AstralBodyRune),
        typeof(AncientWineRune),
        typeof(HolyFireRune),
        typeof(NoNonsenseRune),
        typeof(SuperBrainRune),
        typeof(OverflowRune),
        typeof(SturdyRune),
        typeof(LoopRune),
        typeof(RedEnvelopeRune),
        typeof(MindPurificationRune),
        typeof(EndlessRecoveryRune),
        typeof(SpeedsterRune),
        typeof(ServantMasterRune),
        typeof(SoulEaterRune),
        typeof(DonationRune),
        typeof(TwiceThriceRune),
        typeof(FirebrandRune),
        typeof(NightstalkingRune),
        typeof(GetExcitedRune),
        typeof(ShrinkEngineRune),
        typeof(TransmutePrismaticRune),
        typeof(DawnbringersResolveRune),
        typeof(ShrinkRayRune)
    ];

    private static readonly IReadOnlyList<Type> PrismaticRuneTypes =
    [
        typeof(EurekaRune),
        typeof(InfiniteLoopRune),
        typeof(SlowCookRune),
        typeof(GiantSlayerRune),
        typeof(CourageOfColossusRune),
        typeof(GlassCannonRune),
        typeof(FinalFormRune),
        typeof(BackToBasicsRune),
        typeof(DrawYourSwordRune),
        typeof(FeelTheBurnRune),
        typeof(MadScientistRune),
        typeof(JeweledGauntletRune),
        typeof(HailToTheKingRune),
        typeof(ArcanePunchRune),
        typeof(PandorasBoxRune),
        typeof(TapDanceRune),
        typeof(InfernalConduitRune),
        typeof(DualWieldRune),
        typeof(GoliathRune),
        typeof(MasterOfDualityRune),
        typeof(HandOfBaronRune),
        typeof(CantTouchThisRune),
        typeof(QueenRune),
        typeof(UltimateRefreshRune),
        typeof(GoldrendRune),
        typeof(TransmuteChaosRune)
    ];

    private static readonly IReadOnlyList<Type> AttributeConversionExclusiveRuneTypes =
    [
        typeof(DexterityToStrengthRune),
        typeof(StrengthToDexterityRune),
        typeof(DexterityStrengthToFocusRune)
    ];

    private static readonly IReadOnlyList<MonsterHexKind> SilverMonsterHexes =
    [
        MonsterHexKind.Slap,
        MonsterHexKind.EscapePlan,
        MonsterHexKind.HeavyHitter,
        MonsterHexKind.BigStrength,
        MonsterHexKind.Tormentor,
        MonsterHexKind.ProtectiveVeil,
        MonsterHexKind.Repulsor,
        MonsterHexKind.Thornmail,
        MonsterHexKind.LightEmUp,
        MonsterHexKind.MountainSoul
    ];

    private static readonly IReadOnlyList<MonsterHexKind> GoldMonsterHexes =
    [
        MonsterHexKind.Sturdy,
        MonsterHexKind.DawnbringersResolve,
        MonsterHexKind.ShrinkRay,
        MonsterHexKind.Firebrand,
        MonsterHexKind.SuperBrain,
        MonsterHexKind.Nightstalking,
        MonsterHexKind.AstralBody,
        MonsterHexKind.TankEngine,
        MonsterHexKind.ShrinkEngine,
        MonsterHexKind.GetExcited,
        MonsterHexKind.TwiceThrice,
        MonsterHexKind.Loop,
        MonsterHexKind.ServantMaster
    ];

    private static readonly IReadOnlyList<MonsterHexKind> PrismaticMonsterHexes =
    [
        MonsterHexKind.CourageOfColossus,
        MonsterHexKind.GlassCannon,
        MonsterHexKind.Goliath,
        MonsterHexKind.Queen,
        MonsterHexKind.HandOfBaron,
        MonsterHexKind.CantTouchThis,
        MonsterHexKind.MasterOfDuality,
        MonsterHexKind.Goldrend,
        MonsterHexKind.FeelTheBurn,
        MonsterHexKind.BackToBasics,
        MonsterHexKind.MadScientist
    ];

    private static readonly IReadOnlyList<Type> AllRuneTypes = SilverRuneTypes
        .Concat(GoldRuneTypes)
        .Concat(PrismaticRuneTypes)
        .ToArray();

    public static IReadOnlyList<Type> GetAllRuneTypes() => AllRuneTypes;

    public static IReadOnlyList<Type> GetPlayerRuneTypesForRarity(HextechRarityTier rarity)
    {
        return rarity switch
        {
            HextechRarityTier.Silver => SilverRuneTypes,
            HextechRarityTier.Gold => GoldRuneTypes,
            HextechRarityTier.Prismatic => PrismaticRuneTypes,
            _ => Array.Empty<Type>()
        };
    }

    public static IReadOnlyList<MonsterHexKind> GetMonsterHexesForRarity(HextechRarityTier rarity)
    {
        return rarity switch
        {
            HextechRarityTier.Silver => SilverMonsterHexes,
            HextechRarityTier.Gold => GoldMonsterHexes,
            HextechRarityTier.Prismatic => PrismaticMonsterHexes,
            _ => Array.Empty<MonsterHexKind>()
        };
    }

    public static HextechRarityTier GetMonsterHexRarity(MonsterHexKind hex)
    {
        if (SilverMonsterHexes.Contains(hex))
        {
            return HextechRarityTier.Silver;
        }

        if (GoldMonsterHexes.Contains(hex))
        {
            return HextechRarityTier.Gold;
        }

        if (PrismaticMonsterHexes.Contains(hex))
        {
            return HextechRarityTier.Prismatic;
        }

        throw new ArgumentOutOfRangeException(nameof(hex), hex, "Unknown monster hex rarity.");
    }

    public static RelicModel GetIconRelicForMonsterHex(MonsterHexKind hex)
    {
        return hex switch
        {
            MonsterHexKind.Slap => ModelDb.Relic<SlapRune>(),
            MonsterHexKind.EscapePlan => ModelDb.Relic<EscapePlanRune>(),
            MonsterHexKind.HeavyHitter => ModelDb.Relic<HeavyHitterRune>(),
            MonsterHexKind.BigStrength => ModelDb.Relic<BigStrengthRune>(),
            MonsterHexKind.Tormentor => ModelDb.Relic<TormentorRune>(),
            MonsterHexKind.ProtectiveVeil => ModelDb.Relic<ProtectiveVeilRune>(),
            MonsterHexKind.Repulsor => ModelDb.Relic<RepulsorRune>(),
            MonsterHexKind.Thornmail => ModelDb.Relic<ThornmailRune>(),
            MonsterHexKind.LightEmUp => ModelDb.Relic<LightEmUpRune>(),
            MonsterHexKind.MountainSoul => ModelDb.Relic<MountainSoulRune>(),
            MonsterHexKind.Sturdy => ModelDb.Relic<SturdyRune>(),
            MonsterHexKind.DawnbringersResolve => ModelDb.Relic<DawnbringersResolveRune>(),
            MonsterHexKind.ShrinkRay => ModelDb.Relic<ShrinkRayRune>(),
            MonsterHexKind.Firebrand => ModelDb.Relic<FirebrandRune>(),
            MonsterHexKind.SuperBrain => ModelDb.Relic<SuperBrainRune>(),
            MonsterHexKind.AstralBody => ModelDb.Relic<AstralBodyRune>(),
            MonsterHexKind.Nightstalking => ModelDb.Relic<NightstalkingRune>(),
            MonsterHexKind.TankEngine => ModelDb.Relic<TankEngineRune>(),
            MonsterHexKind.ShrinkEngine => ModelDb.Relic<ShrinkEngineRune>(),
            MonsterHexKind.GetExcited => ModelDb.Relic<GetExcitedRune>(),
            MonsterHexKind.TwiceThrice => ModelDb.Relic<TwiceThriceRune>(),
            MonsterHexKind.Loop => ModelDb.Relic<LoopRune>(),
            MonsterHexKind.ServantMaster => ModelDb.Relic<ServantMasterRune>(),
            MonsterHexKind.CourageOfColossus => ModelDb.Relic<CourageOfColossusRune>(),
            MonsterHexKind.GlassCannon => ModelDb.Relic<GlassCannonRune>(),
            MonsterHexKind.Goliath => ModelDb.Relic<GoliathRune>(),
            MonsterHexKind.Queen => ModelDb.Relic<QueenRune>(),
            MonsterHexKind.HandOfBaron => ModelDb.Relic<HandOfBaronRune>(),
            MonsterHexKind.CantTouchThis => ModelDb.Relic<CantTouchThisRune>(),
            MonsterHexKind.MasterOfDuality => ModelDb.Relic<MasterOfDualityRune>(),
            MonsterHexKind.Goldrend => ModelDb.Relic<GoldrendRune>(),
            MonsterHexKind.FeelTheBurn => ModelDb.Relic<FeelTheBurnRune>(),
            MonsterHexKind.BackToBasics => ModelDb.Relic<BackToBasicsRune>(),
            MonsterHexKind.DrawYourSword => ModelDb.Relic<DrawYourSwordRune>(),
            MonsterHexKind.MadScientist => ModelDb.Relic<MadScientistRune>(),
            _ => ModelDb.Relic<JudicatorRune>()
        };
    }

    public static bool TryGetMonsterHexKind(RelicModel relic, out MonsterHexKind hex)
    {
        ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
        foreach (MonsterHexKind candidate in Enum.GetValues<MonsterHexKind>())
        {
            if ((GetIconRelicForMonsterHex(candidate).CanonicalInstance?.Id ?? GetIconRelicForMonsterHex(candidate).Id) == id)
            {
                hex = candidate;
                return true;
            }
        }

        hex = default;
        return false;
    }

    public static string GetEnemyHexDescriptionFormatted(MonsterHexKind hex)
    {
        int playerCount = GetCurrentMultiplayerPlayerCount();
        string? multiplayerKey = playerCount > 1 ? GetEnemyHexMultiplayerDescriptionKey(hex) : null;
        if (multiplayerKey == null)
        {
            return GetEnemyHexDescriptionLoc(hex).GetFormattedText();
        }

        return FormatEnemyHexMultiplayerDescription(
            new LocString("relics", multiplayerKey).GetFormattedText(),
            playerCount);
    }

    public static IEnumerable<IHoverTip> GetEnemyHexHoverTips(MonsterHexKind hex)
    {
        RelicModel relic = GetIconRelicForMonsterHex(hex);
        HoverTip mainTip = new(relic.Title, GetEnemyHexDescriptionFormatted(hex), relic.Icon);
        return [mainTip];
    }

    private static LocString GetEnemyHexDescriptionLoc(MonsterHexKind hex)
    {
        RelicModel relic = GetIconRelicForMonsterHex(hex);
        ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
        return new LocString("relics", ToImageFileStem(id.Entry) + ".enemyDescription");
    }

    private static int GetCurrentMultiplayerPlayerCount()
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        return Math.Max(1, runState?.Players.Count ?? 1);
    }

    private static string? GetEnemyHexMultiplayerDescriptionKey(MonsterHexKind hex)
    {
        return hex switch
        {
            MonsterHexKind.ProtectiveVeil => "protectiveVeilRune.enemyDescriptionMultiplayer",
            MonsterHexKind.Repulsor => "repulsorRune.enemyDescriptionMultiplayer",
            MonsterHexKind.ShrinkEngine => "shrinkEngineRune.enemyDescriptionMultiplayer",
            MonsterHexKind.CourageOfColossus => "courageOfColossusRune.enemyDescriptionMultiplayer",
            MonsterHexKind.CantTouchThis => "cantTouchThisRune.enemyDescriptionMultiplayer",
            _ => null
        };
    }

    private static string FormatEnemyHexMultiplayerDescription(string text, int playerCount)
    {
        return text
            .Replace("$N$", playerCount.ToString(CultureInfo.InvariantCulture))
            .Replace("$PLATING$", (playerCount * 3).ToString(CultureInfo.InvariantCulture))
            .Replace("$BUFFER$", (playerCount * 2).ToString(CultureInfo.InvariantCulture));
    }

    public static IReadOnlyList<RelicModel> GetCanonicalRunes()
    {
        return AllRuneTypes
            .Select(static type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)))
            .ToArray();
    }

    public static bool IsHextechRelic(RelicModel? relic)
    {
        if (relic == null)
        {
            return false;
        }

        ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
        return AllRuneTypes.Any(type => id == ModelDb.GetId(type));
    }

    public static bool IsAvailableForPlayer(RelicModel relic, Player player)
    {
        return relic is not HextechRelicBase hextechRelic || hextechRelic.IsAvailableForPlayer(player);
    }

    public static IReadOnlySet<ModelId> GetMutuallyExclusivePlayerRuneIds(IEnumerable<ModelId> ownedIds)
    {
        HashSet<ModelId> ownedSet = ownedIds.ToHashSet();
        if (!ownedSet.Any(IsAttributeConversionExclusiveRuneId))
        {
            return new HashSet<ModelId>();
        }

        HashSet<ModelId> blockedIds = new();
        foreach (Type runeType in AttributeConversionExclusiveRuneTypes)
        {
            ModelId candidateId = ModelDb.GetId(runeType);
            if (!ownedSet.Contains(candidateId))
            {
                blockedIds.Add(candidateId);
            }
        }

        return blockedIds;
    }

    public static string? TryGetCustomRelicIconPath(RelicModel relic)
    {
        if (!IsHextechRelic(relic))
        {
            return null;
        }

        ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
        return $"res://{Id}/images/relics/{ToImageFileStem(id.Entry)}.png";
    }

    private static string ToImageFileStem(string entry)
    {
        string[] parts = entry.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return entry;
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static bool IsAttributeConversionExclusiveRuneId(ModelId id)
    {
        return AttributeConversionExclusiveRuneTypes.Any(type => ModelDb.GetId(type) == id);
    }

    public static IReadOnlyList<RuneSeriesGroup> GetRuneSeriesGroups(IReadOnlyList<RelicModel> relics)
    {
        Dictionary<ModelId, RelicModel> byId = relics.ToDictionary(static relic => relic.CanonicalInstance?.Id ?? relic.Id);

        IReadOnlyList<RelicModel> BuildGroup(IEnumerable<Type> runeTypes)
        {
            List<RelicModel> group = new();
            foreach (Type runeType in runeTypes)
            {
                ModelId id = ModelDb.GetId(runeType);
                if (byId.TryGetValue(id, out RelicModel? relic))
                {
                    group.Add(relic);
                }
            }

            return group;
        }

        return
        [
            new RuneSeriesGroup("SILVER", BuildGroup(SilverRuneTypes)),
            new RuneSeriesGroup("GOLD", BuildGroup(GoldRuneTypes)),
            new RuneSeriesGroup("PRISMATIC", BuildGroup(PrismaticRuneTypes))
        ];
    }
}
