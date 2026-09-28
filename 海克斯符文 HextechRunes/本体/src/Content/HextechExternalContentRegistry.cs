namespace HextechRunes;

internal static class HextechExternalContentRegistry
{
	internal const string SponsorPackModId = "HextechRunesSponsorPack";

	private static readonly object SyncRoot = new();
	private static readonly List<PlayerRuneRegistration> PlayerRuneRegistrations = new();
	private static readonly List<ForgeRegistration> ForgeRegistrations = new();
	private static readonly List<Type> EventRelicTypes = new();
	private static readonly Dictionary<ModelId, string> AssetModIdsByModelId = new();
	private static readonly Dictionary<ModelId, string> EnchantmentIconPathsByModelId = new();
	private static readonly Dictionary<ModelId, Func<Player, bool>> PlayerRuneAvailabilityByModelId = new();
	// 以下两项只影响界面文字，不进候选池计算，改动不递增 _version。
	private static readonly Dictionary<ModelId, string> PlayerRunePoolLabelKeysByModelId = new();
	private static readonly Dictionary<string, string> ConfigSectionTitleKeysByAssetModId = new(StringComparer.Ordinal);
	private static int _version;

	internal static int Version
	{
		get
		{
			lock (SyncRoot)
			{
				return _version;
			}
		}
	}

	internal static void RegisterPlayerRune(
		PlayerRuneRegistration registration,
		string? assetModId,
		Func<Player, bool>? availability = null)
	{
		lock (SyncRoot)
		{
			int existingIndex = PlayerRuneRegistrations.FindIndex(
				existing => HextechModelTypeIdentity.IsSame(existing.Type, registration.Type));
			if (existingIndex < 0)
			{
				PlayerRuneRegistrations.Add(registration);
				TryStoreAssetModId(registration.Type, assetModId);
				TryStorePlayerRuneAvailability(registration.Type, availability);
				_version++;
				return;
			}

			PlayerRuneRegistration existing = PlayerRuneRegistrations[existingIndex];
			string? existingAssetModId = GetStoredAssetModId(registration.Type);
			if (!HasSamePlayerRuneMetadata(existing, registration))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate player rune registration for {registration.Type.FullName}; first metadata retained: "
					+ $"existing=({Describe(existing)}, assetModId={DescribeValue(existingAssetModId)}) "
					+ $"incoming=({Describe(registration)}, assetModId={DescribeValue(assetModId)}) "
					+ $"callerAssembly={registration.Type.Assembly.GetName().Name ?? "<unknown>"}");
			}

			bool assetOwnerStored = TryStoreAssetModId(registration.Type, assetModId);
			bool availabilityStored = TryStorePlayerRuneAvailability(registration.Type, availability);
			if (assetOwnerStored || availabilityStored)
			{
				_version++;
			}
		}
	}

	internal static void RegisterEventRelic(Type relicType, string? assetModId)
	{
		lock (SyncRoot)
		{
			if (!EventRelicTypes.Any(existing => HextechModelTypeIdentity.IsSame(existing, relicType)))
			{
				EventRelicTypes.Add(relicType);
				TryStoreAssetModId(relicType, assetModId);
				_version++;
				return;
			}

			if (TryStoreAssetModId(relicType, assetModId))
			{
				_version++;
			}
		}
	}

	internal static void RegisterForge(ForgeRegistration registration, string? assetModId)
	{
		lock (SyncRoot)
		{
			int existingIndex = ForgeRegistrations.FindIndex(
				existing => HextechModelTypeIdentity.IsSame(existing.Type, registration.Type));
			if (existingIndex < 0)
			{
				ForgeRegistrations.Add(registration);
				TryStoreAssetModId(registration.Type, assetModId);
				_version++;
				return;
			}

			ForgeRegistration existing = ForgeRegistrations[existingIndex];
			string? existingAssetModId = GetStoredAssetModId(registration.Type);
			if (existing.Rarity != registration.Rarity)
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate forge registration for {registration.Type.FullName}; first metadata retained: "
					+ $"existing=(rarity={existing.Rarity}, assetModId={DescribeValue(existingAssetModId)}) "
					+ $"incoming=(rarity={registration.Rarity}, assetModId={DescribeValue(assetModId)}) "
					+ $"callerAssembly={registration.Type.Assembly.GetName().Name ?? "<unknown>"}");
			}

			if (TryStoreAssetModId(registration.Type, assetModId))
			{
				_version++;
			}
		}
	}

	internal static void RegisterEnchantmentIcon(Type enchantmentType, string iconPath)
	{
		lock (SyncRoot)
		{
			ModelId id = ModelDb.GetId(enchantmentType);
			if (EnchantmentIconPathsByModelId.TryGetValue(id, out string? existingPath))
			{
				if (!string.Equals(existingPath, iconPath, StringComparison.Ordinal)
					&& HextechRunLogBudget.TryConsume("external-content.enchantment-icon-conflict", 12))
				{
					Log.Warn(
						$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate enchantment icon registration for {enchantmentType.FullName}; first path retained: "
						+ $"existingPath={DescribeValue(existingPath)} incomingPath={DescribeValue(iconPath)} "
						+ $"callerAssembly={enchantmentType.Assembly.GetName().Name ?? "<unknown>"}");
				}

				return;
			}

			EnchantmentIconPathsByModelId.Add(id, iconPath);
			_version++;
		}
	}

	internal static IReadOnlyList<PlayerRuneRegistration> GetPlayerRuneRegistrations()
	{
		lock (SyncRoot)
		{
			return PlayerRuneRegistrations.ToArray();
		}
	}

	internal static IReadOnlyList<Type> GetEventRelicTypes()
	{
		lock (SyncRoot)
		{
			return EventRelicTypes.ToArray();
		}
	}

	internal static IReadOnlyList<ForgeRegistration> GetForgeRegistrations()
	{
		lock (SyncRoot)
		{
			return ForgeRegistrations.ToArray();
		}
	}

	internal static string? GetAssetModId(ModelId id)
	{
		lock (SyncRoot)
		{
			return AssetModIdsByModelId.TryGetValue(id, out string? modId)
				? modId
					: null;
		}
	}

	internal static void SetPlayerRunePoolLabel(Type runeType, string poolKey)
	{
		lock (SyncRoot)
		{
			TryStoreFirstWriter(
				PlayerRunePoolLabelKeysByModelId,
				ModelDb.GetId(runeType),
				poolKey,
				"external-content.pool-label-conflict",
				$"pool label for {runeType.FullName}");
		}
	}

	internal static void RegisterConfigSectionTitle(string assetModId, string titleKey)
	{
		lock (SyncRoot)
		{
			TryStoreFirstWriter(
				ConfigSectionTitleKeysByAssetModId,
				assetModId,
				titleKey,
				"external-content.config-section-conflict",
				$"config section title for {assetModId}");
		}
	}

	internal static string? GetPlayerRunePoolLabel(ModelId id)
	{
		lock (SyncRoot)
		{
			return PlayerRunePoolLabelKeysByModelId.TryGetValue(id, out string? poolKey)
				? poolKey
				: null;
		}
	}

	internal static string? GetConfigSectionTitleKey(string assetModId)
	{
		lock (SyncRoot)
		{
			return ConfigSectionTitleKeysByAssetModId.TryGetValue(assetModId, out string? titleKey)
				? titleKey
				: null;
		}
	}

	internal static Func<Player, bool>? GetPlayerRuneAvailability(ModelId id)
	{
		lock (SyncRoot)
		{
			return PlayerRuneAvailabilityByModelId.TryGetValue(id, out Func<Player, bool>? availability)
				? availability
				: null;
		}
	}

	internal static string? GetEnchantmentIconPath(ModelId id)
	{
		lock (SyncRoot)
		{
			return EnchantmentIconPathsByModelId.TryGetValue(id, out string? path)
				? path
				: null;
		}
	}

	private static bool TryStoreAssetModId(Type modelType, string? assetModId)
	{
		if (string.IsNullOrWhiteSpace(assetModId))
		{
			return false;
		}

		ModelId id = ModelDb.GetId(modelType);
		if (AssetModIdsByModelId.TryGetValue(id, out string? existingAssetModId))
		{
			if (!string.Equals(existingAssetModId, assetModId, StringComparison.Ordinal)
				&& HextechRunLogBudget.TryConsume("external-content.asset-owner-conflict", 12))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting asset owner registration for {modelType.FullName}; first owner retained: "
					+ $"existingAssetModId={DescribeValue(existingAssetModId)} "
					+ $"incomingAssetModId={DescribeValue(assetModId)} "
					+ $"callerAssembly={modelType.Assembly.GetName().Name ?? "<unknown>"}");
			}

			return false;
		}

		AssetModIdsByModelId.Add(id, assetModId);
		return true;
	}

	// 与资源归属同一口径：首个登记者生效，重复登记只告警，发放池不随模组加载顺序变化。
	private static bool TryStorePlayerRuneAvailability(Type runeType, Func<Player, bool>? availability)
	{
		if (availability == null)
		{
			return false;
		}

		ModelId id = ModelDb.GetId(runeType);
		if (PlayerRuneAvailabilityByModelId.TryGetValue(id, out Func<Player, bool>? existing))
		{
			if (existing != availability
				&& HextechRunLogBudget.TryConsume("external-content.availability-conflict", 12))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate availability predicate for {runeType.FullName}; first predicate retained: "
					+ $"callerAssembly={runeType.Assembly.GetName().Name ?? "<unknown>"}");
			}

			return false;
		}

		PlayerRuneAvailabilityByModelId.Add(id, availability);
		return true;
	}

	private static void TryStoreFirstWriter<TKey>(
		Dictionary<TKey, string> store,
		TKey key,
		string value,
		string logBudgetKey,
		string description)
		where TKey : notnull
	{
		if (store.TryGetValue(key, out string? existing))
		{
			if (!string.Equals(existing, value, StringComparison.Ordinal)
				&& HextechRunLogBudget.TryConsume(logBudgetKey, 12))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate {description}; first value retained: "
					+ $"existing={DescribeValue(existing)} incoming={DescribeValue(value)}");
			}

			return;
		}

		store.Add(key, value);
	}

	private static string? GetStoredAssetModId(Type modelType)
	{
		return AssetModIdsByModelId.TryGetValue(ModelDb.GetId(modelType), out string? assetModId)
			? assetModId
			: null;
	}

	private static bool HasSamePlayerRuneMetadata(
		PlayerRuneRegistration existing,
		PlayerRuneRegistration incoming)
	{
		return existing.Rarity == incoming.Rarity
			&& existing.Flags == incoming.Flags
			&& existing.CharacterPool == incoming.CharacterPool
			&& existing.CharacterOrder == incoming.CharacterOrder
			&& string.Equals(existing.TagKey, incoming.TagKey, StringComparison.Ordinal);
	}

	private static string Describe(PlayerRuneRegistration registration)
	{
		return $"rarity={registration.Rarity}, flags={registration.Flags}, "
			+ $"characterPool={registration.CharacterPool?.ToString() ?? "none"}, "
			+ $"characterOrder={registration.CharacterOrder}, tagKey={DescribeValue(registration.TagKey)}";
	}

	private static string DescribeValue(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
	}
}
