using System.Text.Json;
using System.Text.Json.Serialization;

namespace HextechRunes;

internal static class HextechRuneConfiguration
{
	private const string ConfigFileName = "rune_config.json";
	// v15(0.8.4):一次性强制重置——旧版本配置载入时整体丢弃回默认(含禁用池/数量/权重/重随/价格/总开关)。
	private const int CurrentConfigVersion = 33;
	private const int ForceResetBelowConfigVersion = 15;
	private const int HexActCount = 3;
	private const int MinActHexCount = 0;
	private const int MaxActHexCount = 6;
	public const int InfiniteRerollLimit = -1;
	private const int MinFiniteRerollLimit = 0;
	private const int MaxFiniteRerollLimit = 9;
	private const int MinRarityWeight = 0;
	private const int MaxRarityWeight = 999;
	private const int MinRandomForgeShopPrice = 0;
	private const int MaxRandomForgeShopPrice = 9999;
	private const int DefaultRandomForgeShopPrice = 250;
	private const bool DefaultRandomForgeDirectGrant = false;
	private const bool DefaultPreventConsecutiveSilverRunes = true;
	private const int DefaultGoldenRerollChancePercent = 5;
	private const int MinGoldenRerollChancePercent = 0;
	private const int MaxGoldenRerollChancePercent = 100;
	// 模组总开关默认开启:关闭后本局表现得与原版一致(开局时快照,联机按房主)。
	private const bool DefaultModEnabled = true;
	private static readonly int[] DefaultPlayerHexCountsByAct = [ 1, 1, 1 ];
	private static readonly int[] DefaultEnemyHexCountsByAct = [ 1, 2, 3 ];
	private const int DefaultPlayerRuneRerollLimit = 1;
	private const int DefaultMonsterHexRerollLimit = 1;
	private static readonly HextechRarityWeights DefaultRuneRarityWeights = new(1, 1, 1);
	private static readonly HextechRarityWeights[] DefaultRuneRarityWeightsByAct =
	[
		DefaultRuneRarityWeights,
		DefaultRuneRarityWeights,
		DefaultRuneRarityWeights
	];
	private static readonly HextechForgeRarityWeights DefaultForgeRarityWeights = new(65, 25, 10);
	// v4~v14 的历史迁移段与配套数组已删除:v15(0.8.4)强制重置使 ConfigVersion<15 一律整体回默认,
	// 那些分支永不可达。活跃链从 v16 起。
	// 腐化树枝生成分布加权(攻击40/技能20/能力40)后无限风险可控,转为默认启用。
	private static readonly Type[] Version16DefaultEnabledRuneTypes =
	[
		typeof(CorruptedBranchRune)
	];
	// 感受燃烧/回力OK镖重做为"获得时给卡"(0.8.4 数据驱动重做),转为默认启用。
	private static readonly Type[] Version17DefaultEnabledRuneTypes =
	[
		typeof(FeelTheBurnRune),
		typeof(OkBoomerangRune)
	];
	// 星界躯体改为百分比生命加成(50%)后强度自洽,转为默认启用。
	private static readonly Type[] Version18DefaultEnabledRuneTypes =
	[
		typeof(AstralBodyRune)
	];
	// 设计审查批次:咔咔!(代价先付收益小)/和平主义者(非亡灵自废输出),转为默认禁用。
	private static readonly Type[] Version19DefaultDisabledRuneTypes =
	[
		typeof(KakaRune),
		typeof(PacifistRune)
	];
	// 小猪存钱罐(鼓励挨打赚钱与防御方向相悖)转为默认禁用。
	private static readonly Type[] Version20DefaultDisabledRuneTypes =
	[
		typeof(PiggyBankRune)
	];
	// 升级打击/防御(围绕不该保留的牌做增强,遥测垫底)与验牌(每回合选牌拖慢节奏)转为默认禁用。
	private static readonly Type[] Version21DefaultDisabledRuneTypes =
	[
		typeof(StrikeUpgradeRune),
		typeof(DefendUpgradeRune),
		typeof(CardInspectionRune)
	];
	// 罪恶快感(开局+击杀双重资源滚雪球)转为默认禁用。
	private static readonly Type[] Version22DefaultDisabledRuneTypes =
	[
		typeof(GetExcitedRune)
	];

	// 0.8.5 遥测(69.8万局)选取率垫底批次转为默认禁用:豪猪7.7%/巨像的勇气10.6%/瓦库11.4%/
	// 死亡收割11.5%/最终形态12.8%(全体中位数30.3%)。
	private static readonly Type[] Version23DefaultDisabledRuneTypes =
	[
		typeof(ShoulderVakuRune),
		typeof(PorcupineRune),
		typeof(CourageOfColossusRune),
		typeof(DeathHarvestRune),
		typeof(FinalFormRune)
	];

	// 升级:打击/防御重做为"最高+999且战后升级本场打出过的"(棱彩),转为默认启用。
	private static readonly Type[] Version24DefaultEnabledRuneTypes =
	[
		typeof(StrikeUpgradeRune),
		typeof(DefendUpgradeRune)
	];

	// 安东尼的偏见转为默认启用(0.8.6)。
	private static readonly Type[] Version25DefaultEnabledRuneTypes =
	[
		typeof(AnthonyBiasRune)
	];

	// 高风险或流程偏慢的通用海克斯转为默认禁用;豪猪已在 v23 禁用,不重复覆盖玩家后续选择。
	private static readonly Type[] Version27DefaultDisabledRuneTypes =
	[
		typeof(OmegaRune),
		typeof(OkBoomerangRune),
		typeof(FeyMagicRune),
		typeof(AstralBodyRune)
	];

	// 以进为退转为默认启用。
	private static readonly Type[] Version30DefaultEnabledRuneTypes =
	[
		typeof(AdvanceToRetreatRune)
	];

	// 歪打正着重做为回合开始时按消耗牌堆状态牌生成充能球，转为默认启用。
	private static readonly Type[] Version31DefaultEnabledRuneTypes =
	[
		typeof(HappyAccidentRune)
	];

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static readonly object SyncRoot = new();
	private static RuneConfig _config = new();
	private static bool _loaded;

	public static bool HasDisabledPlayerRunes
	{
		get
		{
			EnsureLoaded();
			lock (SyncRoot)
			{
				return _config.DisabledPlayerRuneIds.Count > 0;
			}
		}
	}

	public static void Initialize()
	{
		EnsureLoaded();
	}

	public static int[] GetEnemyHexCountsByAct()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeEnemyHexCounts(_config.EnemyHexCountsByAct);
		}
	}

	public static int[] GetPlayerHexCountsByAct()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizePlayerHexCounts(_config.PlayerHexCountsByAct);
		}
	}

	public static bool IsPlayerRuneEnabled(RelicModel relic)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		return IsPlayerRuneEnabled(id.Entry);
	}

	public static bool IsPlayerRuneEnabled(string id)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return !_config.DisabledPlayerRuneIds.Contains(id);
		}
	}

	public static IReadOnlySet<string> GetDisabledPlayerRuneIds()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return _config.DisabledPlayerRuneIds.ToHashSet(StringComparer.Ordinal);
		}
	}

	public static IReadOnlySet<string> GetDisabledMonsterHexIds()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeDisabledMonsterHexIds(_config.DisabledMonsterHexIds);
		}
	}

	public static IReadOnlySet<string> GetDisabledForgeIds()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeDisabledForgeIds(_config.DisabledForgeIds);
		}
	}

	public static HextechRunConfigurationSnapshot GetSnapshot()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeSnapshot(new HextechRunConfigurationSnapshot(
				_config.PlayerHexCountsByAct ?? DefaultPlayerHexCountsByAct,
				_config.EnemyHexCountsByAct ?? DefaultEnemyHexCountsByAct,
				_config.PlayerRuneRerollLimit,
				_config.MonsterHexRerollLimit,
				_config.DisabledPlayerRuneIds,
				_config.DisabledMonsterHexIds,
				_config.DisabledForgeIds,
				ToRarityWeightsByAct(_config.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct),
				_config.PreventConsecutiveSilverRunes,
				_config.GoldenRerollChancePercent,
				ToForgeRarityWeights(_config.ForgeRarityWeights, DefaultForgeRarityWeights),
				_config.RandomForgeShopPrice,
				_config.RandomForgeDirectGrant,
				_config.ModEnabled));
		}
	}

	internal static HashSet<string> NormalizeDisabledPlayerRuneIds(IEnumerable<string>? ids)
	{
		return NormalizeConfigDisabledIds(ids);
	}

	internal static HashSet<string> NormalizeDisabledMonsterHexIds(IEnumerable<string>? ids)
	{
		HashSet<string> validIds = HextechContentRegistry.MonsterHexMetadata.EnabledKindsByRarity
			.Values
			.SelectMany(static kinds => kinds)
			.Select(static kind => kind.ToString())
			.ToHashSet(StringComparer.Ordinal);
		return NormalizeStringIds(ids, validIds);
	}

	internal static HashSet<string> NormalizeDisabledForgeIds(IEnumerable<string>? ids)
	{
		return NormalizeConfigStringIds(ids);
	}

	public static IReadOnlySet<string> GetDefaultDisabledPlayerRuneIds()
	{
		return HextechCatalog.GetDefaultDisabledPlayerRuneIds()
			.Select(static id => id.Entry)
			.ToHashSet(StringComparer.Ordinal);
	}

	public static IReadOnlySet<string> GetDefaultDisabledMonsterHexIds()
	{
		return new HashSet<string>(StringComparer.Ordinal);
	}

	public static IReadOnlySet<string> GetDefaultDisabledForgeIds()
	{
		return new HashSet<string>(StringComparer.Ordinal);
	}

	public static void SaveDisabledPlayerRuneIds(IEnumerable<string> disabledIds)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			_config.ConfigVersion = CurrentConfigVersion;
			_config.DisabledPlayerRuneIds = NormalizeConfigDisabledIds(disabledIds);
			SaveConfig(_config);
		}
	}

	public static void SaveEnemyHexCountsByAct(IReadOnlyList<int> counts)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			_config.ConfigVersion = CurrentConfigVersion;
			_config.EnemyHexCountsByAct = NormalizeEnemyHexCounts(counts);
			SaveConfig(_config);
		}
	}

	public static void SaveSnapshot(HextechRunConfigurationSnapshot snapshot)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			HextechRunConfigurationSnapshot normalized = NormalizeSnapshot(snapshot);
			_config.ConfigVersion = CurrentConfigVersion;
			_config.PlayerHexCountsByAct = normalized.PlayerHexCountsByAct;
			_config.EnemyHexCountsByAct = normalized.EnemyHexCountsByAct;
			_config.PlayerRuneRerollLimit = normalized.PlayerRuneRerollLimit;
			_config.MonsterHexRerollLimit = normalized.MonsterHexRerollLimit;
			_config.DisabledPlayerRuneIds = normalized.DisabledPlayerRuneIds;
			_config.DisabledMonsterHexIds = normalized.DisabledMonsterHexIds;
			_config.DisabledForgeIds = normalized.DisabledForgeIds;
			_config.RuneRarityWeightsByAct = FromRarityWeightsByAct(normalized.RuneRarityWeightsByAct);
			_config.RuneRarityWeights = null;
			_config.PreventConsecutiveSilverRunes = normalized.PreventConsecutiveSilverRunes;
			_config.GoldenRerollChancePercent = normalized.GoldenRerollChancePercent;
			_config.FirstActRuneRarityWeights = null;
			_config.NormalRuneRarityWeights = null;
			_config.SecondActAfterSilverRuneRarityWeights = null;
			_config.ForgeRarityWeights = FromForgeRarityWeights(normalized.ForgeRarityWeights);
			_config.RandomForgeShopPrice = normalized.RandomForgeShopPrice;
			_config.RandomForgeDirectGrant = normalized.RandomForgeDirectGrant;
			_config.ModEnabled = normalized.ModEnabled;
			SaveConfig(_config);
		}
	}

	// 模组总开关的当前(实时)配置值。运行中应优先读「本局冻结快照」,仅在无 run 场景(菜单外/商店初始化兜底)用它。
	public static bool GetModEnabled()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return _config.ModEnabled;
		}
	}

	public static bool GetDefaultModEnabled()
	{
		return DefaultModEnabled;
	}

	private static void EnsureLoaded()
	{
		lock (SyncRoot)
		{
			if (_loaded)
			{
				return;
			}

			_config = LoadOrCreateConfig();
			_loaded = true;
		}
	}

	private static RuneConfig LoadOrCreateConfig()
	{
		string? configPath = null;
		try
		{
			configPath = GetConfigPath();
			if (!File.Exists(configPath))
			{
				RuneConfig defaultConfig = CreateDefaultConfig();
				SaveConfig(defaultConfig);
				return defaultConfig;
			}

			RuneConfig? parsed = JsonSerializer.Deserialize<RuneConfig>(File.ReadAllText(configPath), JsonOptions);
			RuneConfig config = NormalizeLoadedConfig(parsed ?? new RuneConfig());
			SaveConfig(config);
			return config;
		}
		catch (JsonException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config JSON is invalid; using defaults: {ex.Message}", 2);
			RuneConfig config = CreateDefaultConfig();
			if (configPath != null && TryBackupCorruptConfig(configPath))
			{
				SaveConfig(config);
			}

			return config;
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config read was denied; using in-memory defaults without overwriting the file: {ex.Message}", 2);
			return CreateDefaultConfig();
		}
		catch (IOException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config read failed due to I/O; using in-memory defaults without overwriting the file: {ex.Message}", 2);
			return CreateDefaultConfig();
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][RuneConfig] Unexpected config read failure; using in-memory defaults without overwriting the file: {ex}");
			return CreateDefaultConfig();
		}
	}

	private static bool TryBackupCorruptConfig(string configPath)
	{
		try
		{
			File.Copy(configPath, configPath + ".corrupt.bak", overwrite: true);
			return true;
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Could not back up corrupt config; original file will not be overwritten: {ex.Message}", 2);
			return false;
		}
		catch (IOException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Could not back up corrupt config; original file will not be overwritten: {ex.Message}", 2);
			return false;
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][RuneConfig] Unexpected corrupt-config backup failure; original file will not be overwritten: {ex}");
			return false;
		}
	}

	private static RuneConfig CreateDefaultConfig()
	{
		return new RuneConfig
		{
			ConfigVersion = CurrentConfigVersion,
			DisabledPlayerRuneIds = GetDefaultDisabledPlayerRuneIds().ToHashSet(StringComparer.Ordinal),
			PlayerHexCountsByAct = NormalizePlayerHexCounts(null),
			EnemyHexCountsByAct = NormalizeEnemyHexCounts(null),
			PlayerRuneRerollLimit = DefaultPlayerRuneRerollLimit,
			MonsterHexRerollLimit = DefaultMonsterHexRerollLimit,
			DisabledMonsterHexIds = GetDefaultDisabledMonsterHexIds().ToHashSet(StringComparer.Ordinal),
			DisabledForgeIds = GetDefaultDisabledForgeIds().ToHashSet(StringComparer.Ordinal),
			RuneRarityWeightsByAct = FromRarityWeightsByAct(DefaultRuneRarityWeightsByAct),
			PreventConsecutiveSilverRunes = DefaultPreventConsecutiveSilverRunes,
			GoldenRerollChancePercent = DefaultGoldenRerollChancePercent,
			ForgeRarityWeights = FromForgeRarityWeights(DefaultForgeRarityWeights),
			RandomForgeShopPrice = DefaultRandomForgeShopPrice,
			RandomForgeDirectGrant = DefaultRandomForgeDirectGrant,
			ModEnabled = DefaultModEnabled
		};
	}

	private static RuneConfig NormalizeLoadedConfig(RuneConfig config)
	{
		// 0.8.4 一次性强制回默认:旧配置(含用户自定义)整体丢弃,不走增量迁移链。
		if (config.ConfigVersion < ForceResetBelowConfigVersion)
		{
			HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Config version {config.ConfigVersion} < {ForceResetBelowConfigVersion}; forcing full reset to defaults (0.8.4).");
			return CreateDefaultConfig();
		}

		int previousConfigVersion = config.ConfigVersion;
		HashSet<string> disabledIds = NormalizeConfigDisabledIds(config.DisabledPlayerRuneIds);
		HashSet<string> disabledMonsterHexIds = NormalizeDisabledMonsterHexIds(config.DisabledMonsterHexIds);
		if (previousConfigVersion < 16)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version16DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 17)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version17DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 18)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version18DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 19)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version19DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 20)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version20DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 21)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version21DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 22)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version22DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 23)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version23DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 24)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version24DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 25)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version25DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 27)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version27DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 28)
		{
			config.RuneRarityWeights = config.NormalRuneRarityWeights;
			config.PreventConsecutiveSilverRunes = DefaultPreventConsecutiveSilverRunes;
		}

		if (previousConfigVersion < 29)
		{
			config.GoldenRerollChancePercent = DefaultGoldenRerollChancePercent;
		}

		if (previousConfigVersion < 30)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version30DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 31)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version31DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 32)
		{
			HextechRarityWeights legacyWeights = ToRarityWeights(config.RuneRarityWeights, DefaultRuneRarityWeights);
			config.RuneRarityWeightsByAct = FromRarityWeightsByAct([ legacyWeights, legacyWeights, legacyWeights ]);
		}

		if (previousConfigVersion < 33 && config.MonsterHexRerollLimit == InfiniteRerollLimit)
		{
			config.MonsterHexRerollLimit = DefaultMonsterHexRerollLimit;
		}

		config.ConfigVersion = CurrentConfigVersion;
		config.DisabledPlayerRuneIds = disabledIds;
		config.PlayerHexCountsByAct = NormalizePlayerHexCounts(config.PlayerHexCountsByAct);
		config.EnemyHexCountsByAct = NormalizeEnemyHexCounts(config.EnemyHexCountsByAct);
		config.PlayerRuneRerollLimit = ClampRerollLimit(config.PlayerRuneRerollLimit);
		config.MonsterHexRerollLimit = ClampRerollLimit(config.MonsterHexRerollLimit);
		config.DisabledMonsterHexIds = disabledMonsterHexIds;
		config.DisabledForgeIds = NormalizeDisabledForgeIds(config.DisabledForgeIds);
		config.RuneRarityWeightsByAct = FromRarityWeightsByAct(NormalizeRarityWeightsByAct(
			ToRarityWeightsByAct(config.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct),
			DefaultRuneRarityWeightsByAct));
		config.RuneRarityWeights = null;
		config.GoldenRerollChancePercent = ClampGoldenRerollChancePercent(config.GoldenRerollChancePercent);
		config.FirstActRuneRarityWeights = null;
		config.NormalRuneRarityWeights = null;
		config.SecondActAfterSilverRuneRarityWeights = null;
		config.ForgeRarityWeights = FromForgeRarityWeights(NormalizeForgeRarityWeights(
			ToForgeRarityWeights(config.ForgeRarityWeights, DefaultForgeRarityWeights),
			DefaultForgeRarityWeights));
		config.RandomForgeShopPrice = ClampRandomForgeShopPrice(config.RandomForgeShopPrice);
		return config;
	}

	public static int[] GetDefaultPlayerHexCountsByAct()
	{
		return NormalizePlayerHexCounts(null);
	}

	public static int[] GetDefaultEnemyHexCountsByAct()
	{
		return NormalizeEnemyHexCounts(null);
	}

	public static int ClampEnemyHexCount(int count)
	{
		return ClampActHexCount(count);
	}

	public static int ClampPlayerHexCount(int count)
	{
		return ClampActHexCount(count);
	}

	public static int ClampActHexCount(int count)
	{
		return Math.Clamp(count, MinActHexCount, MaxActHexCount);
	}

	public static int ClampRerollLimit(int limit)
	{
		return limit == InfiniteRerollLimit
			? InfiniteRerollLimit
			: Math.Clamp(limit, MinFiniteRerollLimit, MaxFiniteRerollLimit);
	}

	public static int StepRerollLimit(int current, int delta)
	{
		current = ClampRerollLimit(current);
		if (delta > 0)
		{
			return current == InfiniteRerollLimit || current >= MaxFiniteRerollLimit
				? InfiniteRerollLimit
				: current + 1;
		}

		if (delta < 0)
		{
			return current == InfiniteRerollLimit
				? MaxFiniteRerollLimit
				: Math.Max(MinFiniteRerollLimit, current - 1);
		}

		return current;
	}

	public static int GetDefaultPlayerRuneRerollLimit()
	{
		return DefaultPlayerRuneRerollLimit;
	}

	public static int GetDefaultMonsterHexRerollLimit()
	{
		return DefaultMonsterHexRerollLimit;
	}

	public static int ClampRarityWeight(int weight)
	{
		return Math.Clamp(weight, MinRarityWeight, MaxRarityWeight);
	}

	public static int ClampRandomForgeShopPrice(int price)
	{
		return Math.Clamp(price, MinRandomForgeShopPrice, MaxRandomForgeShopPrice);
	}

	public static HextechRarityWeights GetDefaultRuneRarityWeights()
	{
		return DefaultRuneRarityWeights;
	}

	public static HextechRarityWeights[] GetDefaultRuneRarityWeightsByAct()
	{
		return DefaultRuneRarityWeightsByAct.ToArray();
	}

	public static bool GetDefaultPreventConsecutiveSilverRunes()
	{
		return DefaultPreventConsecutiveSilverRunes;
	}

	public static int GetDefaultGoldenRerollChancePercent()
	{
		return DefaultGoldenRerollChancePercent;
	}

	public static int ClampGoldenRerollChancePercent(int percent)
	{
		return Math.Clamp(percent, MinGoldenRerollChancePercent, MaxGoldenRerollChancePercent);
	}

	public static HextechForgeRarityWeights GetDefaultForgeRarityWeights()
	{
		return DefaultForgeRarityWeights;
	}

	public static int GetDefaultRandomForgeShopPrice()
	{
		return DefaultRandomForgeShopPrice;
	}

	internal static HextechRunConfigurationSnapshot GetDefaultSnapshot()
	{
		return NormalizeSnapshot(new HextechRunConfigurationSnapshot(
			DefaultPlayerHexCountsByAct,
			DefaultEnemyHexCountsByAct,
			DefaultPlayerRuneRerollLimit,
			DefaultMonsterHexRerollLimit,
			GetDefaultDisabledPlayerRuneIds().ToHashSet(StringComparer.Ordinal),
			GetDefaultDisabledMonsterHexIds().ToHashSet(StringComparer.Ordinal),
			GetDefaultDisabledForgeIds().ToHashSet(StringComparer.Ordinal),
			DefaultRuneRarityWeightsByAct,
			DefaultPreventConsecutiveSilverRunes,
			DefaultGoldenRerollChancePercent,
			DefaultForgeRarityWeights,
			DefaultRandomForgeShopPrice,
			DefaultRandomForgeDirectGrant,
			DefaultModEnabled));
	}

	internal static HextechRunConfigurationSnapshot NormalizeSnapshot(HextechRunConfigurationSnapshot snapshot)
	{
		return new HextechRunConfigurationSnapshot(
			NormalizePlayerHexCounts(snapshot.PlayerHexCountsByAct),
			NormalizeEnemyHexCounts(snapshot.EnemyHexCountsByAct),
			ClampRerollLimit(snapshot.PlayerRuneRerollLimit),
			ClampRerollLimit(snapshot.MonsterHexRerollLimit),
			NormalizeDisabledPlayerRuneIds(snapshot.DisabledPlayerRuneIds),
			NormalizeDisabledMonsterHexIds(snapshot.DisabledMonsterHexIds),
			NormalizeDisabledForgeIds(snapshot.DisabledForgeIds),
			NormalizeRarityWeightsByAct(snapshot.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct),
			snapshot.PreventConsecutiveSilverRunes,
			ClampGoldenRerollChancePercent(snapshot.GoldenRerollChancePercent),
			NormalizeForgeRarityWeights(snapshot.ForgeRarityWeights, DefaultForgeRarityWeights),
			ClampRandomForgeShopPrice(snapshot.RandomForgeShopPrice),
			snapshot.RandomForgeDirectGrant,
			snapshot.ModEnabled);
	}

	internal static HextechRarityWeights NormalizeRarityWeights(HextechRarityWeights weights, HextechRarityWeights fallback)
	{
		HextechRarityWeights normalized = new(
			ClampRarityWeight(weights.Silver),
			ClampRarityWeight(weights.Gold),
			ClampRarityWeight(weights.Prismatic));
		return normalized.Total > 0 ? normalized : fallback;
	}

	internal static HextechRarityWeights[] NormalizeRarityWeightsByAct(
		IReadOnlyList<HextechRarityWeights>? weightsByAct,
		IReadOnlyList<HextechRarityWeights> fallbackByAct)
	{
		HextechRarityWeights[] normalized = new HextechRarityWeights[HexActCount];
		for (int actIndex = 0; actIndex < normalized.Length; actIndex++)
		{
			HextechRarityWeights fallback = fallbackByAct[Math.Min(actIndex, fallbackByAct.Count - 1)];
			HextechRarityWeights weights = weightsByAct != null && actIndex < weightsByAct.Count
				? weightsByAct[actIndex]
				: fallback;
			normalized[actIndex] = NormalizeRarityWeights(weights, fallback);
		}

		return normalized;
	}

	internal static HextechForgeRarityWeights NormalizeForgeRarityWeights(HextechForgeRarityWeights weights, HextechForgeRarityWeights fallback)
	{
		HextechForgeRarityWeights normalized = new(
			ClampRarityWeight(weights.Silver),
			ClampRarityWeight(weights.Gold),
			ClampRarityWeight(weights.Prismatic));
		return normalized.Total > 0 ? normalized : fallback;
	}

	internal static int[] NormalizePlayerHexCounts(IReadOnlyList<int>? counts)
	{
		return NormalizeActHexCounts(counts, DefaultPlayerHexCountsByAct);
	}

	private static int[] NormalizeEnemyHexCounts(IReadOnlyList<int>? counts)
	{
		return NormalizeActHexCounts(counts, DefaultEnemyHexCountsByAct);
	}

	private static int[] NormalizeActHexCounts(IReadOnlyList<int>? counts, IReadOnlyList<int> defaults)
	{
		int[] normalized = defaults.ToArray();
		if (counts == null)
		{
			return normalized;
		}

		for (int i = 0; i < Math.Min(HexActCount, counts.Count); i++)
		{
			normalized[i] = ClampActHexCount(counts[i]);
		}

		return normalized;
	}

	// 测试钩子:用真实迁移链跑一份合成配置,返回迁移后的版本号与禁用集(仅 HextechRunes.Tests 使用)。
	internal static (int ConfigVersion, IReadOnlySet<string> DisabledPlayerRuneIds) MigrateDisabledIdsForTests(int configVersion, IEnumerable<string> disabledIds)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			DisabledPlayerRuneIds = disabledIds.ToHashSet(StringComparer.Ordinal)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (normalized.ConfigVersion, normalized.DisabledPlayerRuneIds);
	}

	internal static (int ConfigVersion, IReadOnlySet<string> DisabledMonsterHexIds) MigrateDisabledMonsterHexIdsForTests(
		int configVersion,
		IEnumerable<string> disabledIds)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			DisabledMonsterHexIds = disabledIds.ToHashSet(StringComparer.Ordinal)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (normalized.ConfigVersion, normalized.DisabledMonsterHexIds);
	}

	internal static (int ConfigVersion, HextechRarityWeights RuneRarityWeights, bool PreventConsecutiveSilverRunes) MigrateRarityConfigForTests(
		int configVersion,
		HextechRarityWeights normalWeights,
		HextechRarityWeights afterSilverWeights)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			NormalRuneRarityWeights = FromRarityWeights(normalWeights),
			SecondActAfterSilverRuneRarityWeights = FromRarityWeights(afterSilverWeights)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (
			normalized.ConfigVersion,
			ToRarityWeightsByAct(normalized.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct)[0],
			normalized.PreventConsecutiveSilverRunes);
	}

	internal static HextechRarityWeights[] MigrateSingleRarityConfigForTests(
		int configVersion,
		HextechRarityWeights weights)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			RuneRarityWeights = FromRarityWeights(weights)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return ToRarityWeightsByAct(normalized.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct);
	}

	internal static (int ConfigVersion, int MonsterHexRerollLimit) MigrateMonsterHexRerollLimitForTests(
		int configVersion,
		int rerollLimit)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			MonsterHexRerollLimit = rerollLimit
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (normalized.ConfigVersion, normalized.MonsterHexRerollLimit);
	}

	private static HashSet<string> NormalizeConfigDisabledIds(IEnumerable<string>? ids)
	{
		return HextechPlayerRuneConfigIds.Normalize(ids);
	}

	private static HashSet<string> NormalizeStringIds(IEnumerable<string>? ids, IReadOnlySet<string> validIds)
	{
		return (ids ?? [])
			.Where(static id => !string.IsNullOrWhiteSpace(id))
			.Select(static id => id.Trim())
			.Distinct(StringComparer.Ordinal)
			.Where(validIds.Contains)
			.OrderBy(static id => id, StringComparer.Ordinal)
			.ToHashSet(StringComparer.Ordinal);
	}

	private static HashSet<string> NormalizeConfigStringIds(IEnumerable<string>? ids)
	{
		return (ids ?? [])
			.Where(static id => !string.IsNullOrWhiteSpace(id))
			.Select(static id => id.Trim())
			.Distinct(StringComparer.Ordinal)
			.OrderBy(static id => id, StringComparer.Ordinal)
			.ToHashSet(StringComparer.Ordinal);
	}

	private static HashSet<string> GetPlayerRuneIds(IEnumerable<Type> runeTypes)
	{
		return HextechPlayerRuneConfigIds.FromTypes(runeTypes);
	}

	private static HextechRarityWeights ToRarityWeights(RarityWeightConfig? config, HextechRarityWeights fallback)
	{
		return config == null
			? fallback
			: new HextechRarityWeights(config.Silver, config.Gold, config.Prismatic);
	}

	private static HextechRarityWeights[] ToRarityWeightsByAct(
		IReadOnlyList<RarityWeightConfig>? configs,
		IReadOnlyList<HextechRarityWeights> fallback)
	{
		return Enumerable.Range(0, HexActCount)
			.Select(actIndex => configs != null && actIndex < configs.Count
				? ToRarityWeights(configs[actIndex], fallback[Math.Min(actIndex, fallback.Count - 1)])
				: fallback[Math.Min(actIndex, fallback.Count - 1)])
			.ToArray();
	}

	private static HextechForgeRarityWeights ToForgeRarityWeights(RarityWeightConfig? config, HextechForgeRarityWeights fallback)
	{
		return config == null
			? fallback
			: new HextechForgeRarityWeights(config.Silver, config.Gold, config.Prismatic);
	}

	private static RarityWeightConfig FromRarityWeights(HextechRarityWeights weights)
	{
		return new RarityWeightConfig
		{
			Silver = weights.Silver,
			Gold = weights.Gold,
			Prismatic = weights.Prismatic
		};
	}

	private static RarityWeightConfig[] FromRarityWeightsByAct(IEnumerable<HextechRarityWeights> weightsByAct)
	{
		return weightsByAct.Select(FromRarityWeights).ToArray();
	}

	private static RarityWeightConfig FromForgeRarityWeights(HextechForgeRarityWeights weights)
	{
		return new RarityWeightConfig
		{
			Silver = weights.Silver,
			Gold = weights.Gold,
			Prismatic = weights.Prismatic
		};
	}

	private static void SaveConfig(RuneConfig config)
	{
		try
		{
			string configPath = GetConfigPath();
			Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
			string serialized = JsonSerializer.Serialize(config, JsonOptions);
			File.WriteAllText(configPath, serialized);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config write failed: {ex.Message}", 2);
		}
	}

	private static string GetConfigPath()
	{
		return HextechDataPaths.GetFilePath(ConfigFileName);
	}

	private sealed class RuneConfig
	{
		[JsonPropertyName("config_version")]
		public int ConfigVersion { get; set; }

		[JsonPropertyName("disabled_player_rune_ids")]
		public HashSet<string> DisabledPlayerRuneIds { get; set; } = new(StringComparer.Ordinal);

		[JsonPropertyName("player_hex_counts_by_act")]
		public int[]? PlayerHexCountsByAct { get; set; }

		[JsonPropertyName("enemy_hex_counts_by_act")]
		public int[]? EnemyHexCountsByAct { get; set; }

		[JsonPropertyName("player_rune_reroll_limit")]
		public int PlayerRuneRerollLimit { get; set; } = DefaultPlayerRuneRerollLimit;

		[JsonPropertyName("monster_hex_reroll_limit")]
		public int MonsterHexRerollLimit { get; set; } = DefaultMonsterHexRerollLimit;

		[JsonPropertyName("disabled_monster_hex_ids")]
		public HashSet<string> DisabledMonsterHexIds { get; set; } = new(StringComparer.Ordinal);

		[JsonPropertyName("disabled_forge_ids")]
		public HashSet<string> DisabledForgeIds { get; set; } = new(StringComparer.Ordinal);

		[JsonPropertyName("rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? RuneRarityWeights { get; set; }

		[JsonPropertyName("rune_rarity_weights_by_act")]
		public RarityWeightConfig[]? RuneRarityWeightsByAct { get; set; }

		[JsonPropertyName("prevent_consecutive_silver_runes")]
		public bool PreventConsecutiveSilverRunes { get; set; } = DefaultPreventConsecutiveSilverRunes;

		[JsonPropertyName("golden_reroll_chance_percent")]
		public int GoldenRerollChancePercent { get; set; } = DefaultGoldenRerollChancePercent;

		[JsonPropertyName("first_act_rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? FirstActRuneRarityWeights { get; set; }

		[JsonPropertyName("normal_rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? NormalRuneRarityWeights { get; set; }

		[JsonPropertyName("second_act_after_silver_rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? SecondActAfterSilverRuneRarityWeights { get; set; }

		[JsonPropertyName("forge_rarity_weights")]
		public RarityWeightConfig? ForgeRarityWeights { get; set; }

		[JsonPropertyName("random_forge_shop_price")]
		public int RandomForgeShopPrice { get; set; } = DefaultRandomForgeShopPrice;

		[JsonPropertyName("random_forge_direct_grant")]
		public bool RandomForgeDirectGrant { get; set; } = DefaultRandomForgeDirectGrant;

		[JsonPropertyName("mod_enabled")]
		public bool ModEnabled { get; set; } = DefaultModEnabled;
	}

	private sealed class RarityWeightConfig
	{
		[JsonPropertyName("silver")]
		public int Silver { get; set; }

		[JsonPropertyName("gold")]
		public int Gold { get; set; }

		[JsonPropertyName("prismatic")]
		public int Prismatic { get; set; }
	}
}
