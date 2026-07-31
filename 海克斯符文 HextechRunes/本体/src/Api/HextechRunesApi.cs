namespace HextechRunes;

public static class HextechRunesApi
{
	public const string PersistentInnateMarkerSavedPropertyName = "SavedCosplayInnateMarker";

	/// <summary>
	/// 注册外部玩家符文。必须在模组初始化阶段、共享遗物池首次枚举前调用。
	/// </summary>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterPlayerRune<TRune>(
		HextechRarityTier rarity,
		PlayerRuneFlags flags = PlayerRuneFlags.None,
		PlayerRuneCharacterPool? characterPool = null,
		int characterOrder = 0,
		string tagKey = "COMPREHENSIVE",
		string? assetModId = null)
		where TRune : HextechRelicBase
	{
		RegisterPlayerRune(typeof(TRune), rarity, flags, characterPool, characterOrder, tagKey, assetModId);
	}

	/// <summary>
	/// 注册外部玩家符文。必须在模组初始化阶段、共享遗物池首次枚举前调用。
	/// </summary>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterPlayerRune(
		Type runeType,
		HextechRarityTier rarity,
		PlayerRuneFlags flags = PlayerRuneFlags.None,
		PlayerRuneCharacterPool? characterPool = null,
		int characterOrder = 0,
		string tagKey = "COMPREHENSIVE",
		string? assetModId = null)
	{
		if (runeType.IsAbstract || !typeof(HextechRelicBase).IsAssignableFrom(runeType))
		{
			throw new ArgumentException($"Player rune type must be a concrete {nameof(HextechRelicBase)}: {runeType.FullName}", nameof(runeType));
		}

		PlayerRuneRegistration registration = new(runeType, rarity, flags, characterPool, characterOrder, tagKey);
		HextechSavedPropertyBootstrap.EnsureModelTypeRegistrationAllowed(runeType);
		HextechCatalog.EnsureExternalModelIdAvailable(runeType);
		HextechCatalog.EnsureConfigurablePlayerRuneIdEntryAvailable(runeType);
		HextechModelPoolRegistrar.RegisterPlayerRuneModels([ runeType ]);
		HextechSavedPropertyBootstrap.InjectModelType(runeType);
		HextechExternalContentRegistry.RegisterPlayerRune(registration, assetModId);
	}

	/// <summary>
	/// 注册外部事件遗物。必须在模组初始化阶段、事件遗物池首次枚举前调用。
	/// </summary>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterEventRelic<TRelic>(string? assetModId = null)
		where TRelic : RelicModel
	{
		RegisterEventRelic(typeof(TRelic), assetModId);
	}

	/// <summary>
	/// 注册外部事件遗物。必须在模组初始化阶段、事件遗物池首次枚举前调用。
	/// </summary>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterEventRelic(Type relicType, string? assetModId = null)
	{
		if (relicType.IsAbstract || !typeof(RelicModel).IsAssignableFrom(relicType))
		{
			throw new ArgumentException($"Event relic type must be a concrete {nameof(RelicModel)}: {relicType.FullName}", nameof(relicType));
		}

		HextechSavedPropertyBootstrap.EnsureModelTypeRegistrationAllowed(relicType);
		HextechCatalog.EnsureExternalModelIdAvailable(relicType);
		HextechModelPoolRegistrar.RegisterEventRelicModels([ relicType ]);
		HextechSavedPropertyBootstrap.InjectModelType(relicType);
		HextechExternalContentRegistry.RegisterEventRelic(relicType, assetModId);
	}

	/// <summary>
	/// 注册外部锻造。必须在模组初始化阶段、共享遗物池首次枚举前调用。
	/// </summary>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterForge<TForge>(HextechRarityTier rarity, string? assetModId = null)
		where TForge : HextechForgeBase
	{
		RegisterForge(typeof(TForge), rarity, assetModId);
	}

	/// <summary>
	/// 注册外部锻造。必须在模组初始化阶段、共享遗物池首次枚举前调用。
	/// </summary>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterForge(Type forgeType, HextechRarityTier rarity, string? assetModId = null)
	{
		if (forgeType.IsAbstract || !typeof(HextechForgeBase).IsAssignableFrom(forgeType))
		{
			throw new ArgumentException($"Forge type must be a concrete {nameof(HextechForgeBase)}: {forgeType.FullName}", nameof(forgeType));
		}

		HextechSavedPropertyBootstrap.EnsureModelTypeRegistrationAllowed(forgeType);
		HextechCatalog.EnsureExternalModelIdAvailable(forgeType);
		HextechModelPoolRegistrar.RegisterForgeModels([ forgeType ]);
		HextechSavedPropertyBootstrap.InjectModelType(forgeType);
		HextechExternalContentRegistry.RegisterForge(new ForgeRegistration(forgeType, rarity), assetModId);
	}

	public static Task ObtainRandomForges(
		Player player,
		HextechRarityTier rarity,
		int count,
		Func<Type, bool> forgeTypePredicate,
		string source)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(forgeTypePredicate);
		if (string.IsNullOrWhiteSpace(source))
		{
			throw new ArgumentException("Random forge source must not be empty.", nameof(source));
		}

		return HextechForgeGrantHelper.ObtainRandomForges(player, rarity, count, forgeTypePredicate, source);
	}

	public static Task<RelicModel?> SelectRelicOption(
		Player player,
		IReadOnlyList<RelicModel> options,
		string context,
		bool syncMultiplayerChoice = true)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(options);
		if (string.IsNullOrWhiteSpace(context))
		{
			throw new ArgumentException("Relic option selection context must not be empty.", nameof(context));
		}
		if (options.Count > HextechStableModelIdListCodec.MaxCount)
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				options.Count,
				$"Relic option count must not exceed {HextechStableModelIdListCodec.MaxCount}.");
		}

		return HextechRelicOptionSelectionCoordinator.SelectRelicOption(player, options, context, syncMultiplayerChoice);
	}

	/// <summary>
	/// 显式登记外部 SavedProperty 载体。必须在模型初始化窗口内调用；视觉资源注册不会隐式执行此操作。
	/// </summary>
	/// <exception cref="InvalidOperationException">官方序列化缓存已初始化，且目标载体未被缓存。</exception>
	public static void RegisterSavedPropertyCarrier<TModel>()
		where TModel : AbstractModel
	{
		RegisterSavedPropertyCarrier(typeof(TModel));
	}

	/// <summary>
	/// 显式登记外部 SavedProperty 载体。必须在模型初始化窗口内调用；视觉资源注册不会隐式执行此操作。
	/// </summary>
	/// <exception cref="InvalidOperationException">官方序列化缓存已初始化，且目标载体未被缓存。</exception>
	public static void RegisterSavedPropertyCarrier(Type modelType)
	{
		if (modelType.IsAbstract || !typeof(AbstractModel).IsAssignableFrom(modelType))
		{
			throw new ArgumentException($"SavedProperty carrier type must be a concrete {nameof(AbstractModel)}: {modelType.FullName}", nameof(modelType));
		}

		HextechSavedPropertyBootstrap.InjectModelType(modelType);
	}

	/// <summary>
	/// 仅注册外部附魔图标；若附魔含 SavedProperty，调用方还必须在初始化窗口内显式登记载体。
	/// </summary>
	public static void RegisterEnchantmentIcon<TEnchantment>(string iconPath)
		where TEnchantment : EnchantmentModel
	{
		RegisterEnchantmentIcon(typeof(TEnchantment), iconPath);
	}

	/// <summary>
	/// 仅注册外部附魔图标；若附魔含 SavedProperty，调用方还必须在初始化窗口内显式登记载体。
	/// </summary>
	public static void RegisterEnchantmentIcon(Type enchantmentType, string iconPath)
	{
		if (enchantmentType.IsAbstract || !typeof(EnchantmentModel).IsAssignableFrom(enchantmentType))
		{
			throw new ArgumentException($"Enchantment type must be a concrete {nameof(EnchantmentModel)}: {enchantmentType.FullName}", nameof(enchantmentType));
		}
		if (string.IsNullOrWhiteSpace(iconPath))
		{
			throw new ArgumentException("Enchantment icon path must not be empty.", nameof(iconPath));
		}

		HextechExternalContentRegistry.RegisterEnchantmentIcon(enchantmentType, iconPath);
	}

	public static void TrackPersistentInnate(CardModel? card)
	{
		CosplayInnateKeywordPersistence.Track(card);
	}

	public static bool IsPersistentInnateTracked(CardModel? card)
	{
		return CosplayInnateKeywordPersistence.IsTracked(card);
	}

	public static void RestorePersistentInnate(CardModel card)
	{
		CosplayInnateKeywordPersistence.Restore(card);
	}
}
