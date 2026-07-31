namespace HextechRunes;

internal static class HextechContentRegistry
{
	private static readonly object LookupsLock = new();
	private static RegistryLookups? _lookups;
	private static int _lookupsVersion = -1;

	internal static int Version => HextechExternalContentRegistry.Version;

	private static RegistryLookups Lookups
	{
		get
		{
			int version = Version;
			lock (LookupsLock)
			{
				if (_lookups == null || _lookupsVersion != version)
				{
					_lookups = BuildRegistryLookups();
					_lookupsVersion = version;
				}

				return _lookups;
			}
		}
	}

	internal static IReadOnlyList<Type> SilverRuneTypes => Lookups.SilverRuneTypes;

	internal static IReadOnlyList<Type> GoldRuneTypes => Lookups.GoldRuneTypes;

	internal static IReadOnlyList<Type> PrismaticRuneTypes => Lookups.PrismaticRuneTypes;

	internal static IReadOnlyList<Type> SilverForgeTypes => Lookups.SilverForgeTypes;

	internal static IReadOnlyList<Type> GoldForgeTypes => Lookups.GoldForgeTypes;

	internal static IReadOnlyList<Type> PrismaticForgeTypes => Lookups.PrismaticForgeTypes;

	internal static IReadOnlySet<Type> DisabledPlayerRuneTypes => Lookups.DisabledPlayerRuneTypes;

	internal static IReadOnlySet<Type> SelectionExcludedPlayerRuneTypes => Lookups.SelectionExcludedPlayerRuneTypes;

	internal static IReadOnlyList<Type> IroncladRuneTypes => Lookups.IroncladRuneTypes;

	internal static IReadOnlyList<Type> SilentRuneTypes => Lookups.SilentRuneTypes;

	internal static IReadOnlyList<Type> RegentRuneTypes => Lookups.RegentRuneTypes;

	internal static IReadOnlyList<Type> DefectRuneTypes => Lookups.DefectRuneTypes;

	internal static IReadOnlyList<Type> NecrobinderRuneTypes => Lookups.NecrobinderRuneTypes;

	internal static IReadOnlyList<Type> AttributeConversionExclusiveRuneTypes => Lookups.AttributeConversionExclusiveRuneTypes;

	internal static IReadOnlyDictionary<Type, string> PlayerRuneTagKeys => Lookups.PlayerRuneTagKeys;

	internal static PlayerRuneMetadataCatalog PlayerRuneMetadata => Lookups.PlayerRuneMetadata;

	internal static ForgeMetadataCatalog ForgeMetadata => Lookups.ForgeMetadata;

	internal static MonsterHexMetadataCatalog MonsterHexMetadata => Lookups.MonsterHexMetadata;

	internal static IReadOnlySet<Type> FirstActExcludedRuneTypes => Lookups.FirstActExcludedRuneTypes;

	internal static IReadOnlySet<Type> ThirdActExcludedRuneTypes => Lookups.ThirdActExcludedRuneTypes;

	internal static IReadOnlySet<MonsterHexKind> DisabledMonsterHexes => Lookups.DisabledMonsterHexes;

	internal static IReadOnlySet<MonsterHexKind> MonsterHexesWithBurnHoverTip => Lookups.MonsterHexesWithBurnHoverTip;

	internal static IReadOnlyDictionary<MonsterHexKind, Type> MonsterHexIconRelicTypes => Lookups.MonsterHexIconRelicTypes;

	internal static IReadOnlySet<MonsterHexKind> AllMonsterHexKinds => Lookups.AllMonsterHexKinds;

	internal static IReadOnlyList<MonsterHexKind> SilverMonsterHexes => Lookups.SilverMonsterHexes;

	internal static IReadOnlyList<MonsterHexKind> GoldMonsterHexes => Lookups.GoldMonsterHexes;

	internal static IReadOnlyList<MonsterHexKind> PrismaticMonsterHexes => Lookups.PrismaticMonsterHexes;

	internal static IReadOnlyList<Type> AllRuneTypes => Lookups.AllRuneTypes;

	internal static IReadOnlyList<Type> AllForgeTypes => Lookups.AllForgeTypes;

	internal static IReadOnlyList<Type> AllCustomRelicTypes => Lookups.AllCustomRelicTypes;

	internal static IReadOnlyList<Type> ShopOnlyRelicTypes => HextechCustomModelRegistry.ShopOnlyRelicTypes;

	internal static IReadOnlyList<Type> EnemyHexIconRelicTypes => HextechCustomModelRegistry.EnemyHexIconRelicTypes;

	internal static IReadOnlyList<Type> EventRelicTypes =>
		HextechCustomModelRegistry.EventRelicTypes
			.Concat(HextechExternalContentRegistry.GetEventRelicTypes())
			.ToArray();

	internal static IReadOnlyList<Type> CustomCardTypes => HextechCustomModelRegistry.CustomCardTypes;

	private static RegistryLookups BuildRegistryLookups()
	{
		return new RegistryLookups(
				HextechPlayerRuneRegistry.Registrations
					.Concat(HextechExternalContentRegistry.GetPlayerRuneRegistrations())
					.ToArray(),
				HextechForgeRegistry.Registrations
					.Concat(HextechExternalContentRegistry.GetForgeRegistrations())
					.ToArray(),
				HextechMonsterHexRegistry.Registrations,
				HextechCustomModelRegistry.ShopOnlyRelicTypes,
				HextechCustomModelRegistry.EnemyHexIconRelicTypes);
	}

	private sealed class RegistryLookups
	{
		public RegistryLookups(
			IReadOnlyList<PlayerRuneRegistration> runeRegistrations,
			IReadOnlyList<ForgeRegistration> forgeRegistrations,
			IReadOnlyList<MonsterHexRegistration> monsterHexRegistrations,
			IReadOnlyList<Type> shopOnlyRelicTypes,
			IReadOnlyList<Type> enemyHexIconRelicTypes)
		{
			PlayerRuneMetadata = new PlayerRuneMetadataCatalog(runeRegistrations);
			ForgeMetadata = new ForgeMetadataCatalog(forgeRegistrations);
			MonsterHexMetadata = new MonsterHexMetadataCatalog(monsterHexRegistrations);
			SilverRuneTypes = PlayerRuneMetadata.TypesByRarity[HextechRarityTier.Silver];
			GoldRuneTypes = PlayerRuneMetadata.TypesByRarity[HextechRarityTier.Gold];
			PrismaticRuneTypes = PlayerRuneMetadata.TypesByRarity[HextechRarityTier.Prismatic];
			SilverForgeTypes = ForgeMetadata.TypesByRarity[HextechRarityTier.Silver];
			GoldForgeTypes = ForgeMetadata.TypesByRarity[HextechRarityTier.Gold];
			PrismaticForgeTypes = ForgeMetadata.TypesByRarity[HextechRarityTier.Prismatic];
			DisabledPlayerRuneTypes = PlayerRuneMetadata.TypesByFlag[PlayerRuneFlags.Disabled].ToHashSet();
			SelectionExcludedPlayerRuneTypes = PlayerRuneMetadata.TypesByFlag[PlayerRuneFlags.SelectionExcluded].ToHashSet();
			IroncladRuneTypes = PlayerRuneMetadata.TypesByCharacter[PlayerRuneCharacterPool.Ironclad];
			SilentRuneTypes = PlayerRuneMetadata.TypesByCharacter[PlayerRuneCharacterPool.Silent];
			RegentRuneTypes = PlayerRuneMetadata.TypesByCharacter[PlayerRuneCharacterPool.Regent];
			DefectRuneTypes = PlayerRuneMetadata.TypesByCharacter[PlayerRuneCharacterPool.Defect];
			NecrobinderRuneTypes = PlayerRuneMetadata.TypesByCharacter[PlayerRuneCharacterPool.Necrobinder];
			AttributeConversionExclusiveRuneTypes = PlayerRuneMetadata.TypesByFlag[PlayerRuneFlags.AttributeConversionExclusive];
			PlayerRuneTagKeys = PlayerRuneMetadata.TagKeys;
			FirstActExcludedRuneTypes = PlayerRuneMetadata.TypesByFlag[PlayerRuneFlags.FirstActExcluded].ToHashSet();
			ThirdActExcludedRuneTypes = PlayerRuneMetadata.TypesByFlag[PlayerRuneFlags.ThirdActExcluded].ToHashSet();
			DisabledMonsterHexes = MonsterHexMetadata.DisabledKinds;
			MonsterHexesWithBurnHoverTip = MonsterHexMetadata.BurnHoverTipKinds;
			MonsterHexIconRelicTypes = MonsterHexMetadata.IconRelicTypes;
			AllMonsterHexKinds = MonsterHexMetadata.AllKinds;
			SilverMonsterHexes = MonsterHexMetadata.EnabledKindsByRarity[HextechRarityTier.Silver];
			GoldMonsterHexes = MonsterHexMetadata.EnabledKindsByRarity[HextechRarityTier.Gold];
			PrismaticMonsterHexes = MonsterHexMetadata.EnabledKindsByRarity[HextechRarityTier.Prismatic];
			AllRuneTypes = PlayerRuneMetadata.AllTypes;
			AllForgeTypes = ForgeMetadata.AllTypes;
			AllCustomRelicTypes = AllRuneTypes
				.Concat(AllForgeTypes)
				.Concat(shopOnlyRelicTypes)
				.Concat(enemyHexIconRelicTypes)
				.Distinct()
				.ToArray();
		}

		public IReadOnlyList<Type> SilverRuneTypes { get; }
		public IReadOnlyList<Type> GoldRuneTypes { get; }
		public IReadOnlyList<Type> PrismaticRuneTypes { get; }
		public IReadOnlyList<Type> SilverForgeTypes { get; }
		public IReadOnlyList<Type> GoldForgeTypes { get; }
		public IReadOnlyList<Type> PrismaticForgeTypes { get; }
		public IReadOnlySet<Type> DisabledPlayerRuneTypes { get; }
		public IReadOnlySet<Type> SelectionExcludedPlayerRuneTypes { get; }
		public IReadOnlyList<Type> IroncladRuneTypes { get; }
		public IReadOnlyList<Type> SilentRuneTypes { get; }
		public IReadOnlyList<Type> RegentRuneTypes { get; }
		public IReadOnlyList<Type> DefectRuneTypes { get; }
		public IReadOnlyList<Type> NecrobinderRuneTypes { get; }
		public IReadOnlyList<Type> AttributeConversionExclusiveRuneTypes { get; }
		public IReadOnlyDictionary<Type, string> PlayerRuneTagKeys { get; }
		public PlayerRuneMetadataCatalog PlayerRuneMetadata { get; }
		public ForgeMetadataCatalog ForgeMetadata { get; }
		public MonsterHexMetadataCatalog MonsterHexMetadata { get; }
		public IReadOnlySet<Type> FirstActExcludedRuneTypes { get; }
		public IReadOnlySet<Type> ThirdActExcludedRuneTypes { get; }
		public IReadOnlySet<MonsterHexKind> DisabledMonsterHexes { get; }
		public IReadOnlySet<MonsterHexKind> MonsterHexesWithBurnHoverTip { get; }
		public IReadOnlyDictionary<MonsterHexKind, Type> MonsterHexIconRelicTypes { get; }
		public IReadOnlySet<MonsterHexKind> AllMonsterHexKinds { get; }
		public IReadOnlyList<MonsterHexKind> SilverMonsterHexes { get; }
		public IReadOnlyList<MonsterHexKind> GoldMonsterHexes { get; }
		public IReadOnlyList<MonsterHexKind> PrismaticMonsterHexes { get; }
		public IReadOnlyList<Type> AllRuneTypes { get; }
		public IReadOnlyList<Type> AllForgeTypes { get; }
		public IReadOnlyList<Type> AllCustomRelicTypes { get; }
	}
}
