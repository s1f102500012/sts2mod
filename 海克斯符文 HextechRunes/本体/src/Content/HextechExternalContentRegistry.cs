namespace HextechRunes;

internal static class HextechExternalContentRegistry
{
	private static readonly object SyncRoot = new();
	private static readonly List<PlayerRuneRegistration> PlayerRuneRegistrations = new();
	private static readonly List<ForgeRegistration> ForgeRegistrations = new();
	private static readonly List<Type> EventRelicTypes = new();
	private static readonly Dictionary<ModelId, string> AssetModIdsByModelId = new();
	private static readonly Dictionary<ModelId, string> EnchantmentIconPathsByModelId = new();
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

	internal static void RegisterPlayerRune(PlayerRuneRegistration registration, string? assetModId)
	{
		lock (SyncRoot)
		{
			int existingIndex = PlayerRuneRegistrations.FindIndex(
				existing => HextechModelTypeIdentity.IsSame(existing.Type, registration.Type));
			if (existingIndex < 0)
			{
				PlayerRuneRegistrations.Add(registration);
				StoreAssetModId(registration.Type, assetModId);
				_version++;
				return;
			}

			PlayerRuneRegistration existing = PlayerRuneRegistrations[existingIndex];
			string? existingAssetModId = GetStoredAssetModId(registration.Type);
			if (!HasSamePlayerRuneMetadata(existing, registration)
				|| HasConflictingAssetModId(existingAssetModId, assetModId))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate player rune registration for {registration.Type.FullName}; first metadata retained: "
					+ $"existing=({Describe(existing)}, assetModId={DescribeValue(existingAssetModId)}) "
					+ $"incoming=({Describe(registration)}, assetModId={DescribeValue(assetModId)}) "
					+ $"callerAssembly={registration.Type.Assembly.GetName().Name ?? "<unknown>"}");
			}

			StoreAssetModId(registration.Type, assetModId);
		}
	}

	internal static void RegisterEventRelic(Type relicType, string? assetModId)
	{
		lock (SyncRoot)
		{
			if (!EventRelicTypes.Any(existing => HextechModelTypeIdentity.IsSame(existing, relicType)))
			{
				EventRelicTypes.Add(relicType);
				StoreAssetModId(relicType, assetModId);
				_version++;
				return;
			}

			string? existingAssetModId = GetStoredAssetModId(relicType);
			if (HasConflictingAssetModId(existingAssetModId, assetModId))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate event relic asset registration for {relicType.FullName}: "
					+ $"existingAssetModId={DescribeValue(existingAssetModId)} "
					+ $"incomingAssetModId={DescribeValue(assetModId)} "
					+ $"callerAssembly={relicType.Assembly.GetName().Name ?? "<unknown>"}");
			}

			StoreAssetModId(relicType, assetModId);
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
				StoreAssetModId(registration.Type, assetModId);
				_version++;
				return;
			}

			ForgeRegistration existing = ForgeRegistrations[existingIndex];
			string? existingAssetModId = GetStoredAssetModId(registration.Type);
			if (existing.Rarity != registration.Rarity
				|| HasConflictingAssetModId(existingAssetModId, assetModId))
			{
				Log.Warn(
					$"[{ModInfo.Id}][ExternalContent] Conflicting duplicate forge registration for {registration.Type.FullName}; first metadata retained: "
					+ $"existing=(rarity={existing.Rarity}, assetModId={DescribeValue(existingAssetModId)}) "
					+ $"incoming=(rarity={registration.Rarity}, assetModId={DescribeValue(assetModId)}) "
					+ $"callerAssembly={registration.Type.Assembly.GetName().Name ?? "<unknown>"}");
			}

			StoreAssetModId(registration.Type, assetModId);
		}
	}

	internal static void RegisterEnchantmentIcon(Type enchantmentType, string iconPath)
	{
		lock (SyncRoot)
		{
			EnchantmentIconPathsByModelId[ModelDb.GetId(enchantmentType)] = iconPath;
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

	internal static string? GetEnchantmentIconPath(ModelId id)
	{
		lock (SyncRoot)
		{
			return EnchantmentIconPathsByModelId.TryGetValue(id, out string? path)
				? path
				: null;
		}
	}

	private static void StoreAssetModId(Type modelType, string? assetModId)
	{
		if (string.IsNullOrWhiteSpace(assetModId))
		{
			return;
		}

		AssetModIdsByModelId[ModelDb.GetId(modelType)] = assetModId;
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

	private static bool HasConflictingAssetModId(string? existing, string? incoming)
	{
		return !string.IsNullOrWhiteSpace(existing)
			&& !string.IsNullOrWhiteSpace(incoming)
			&& !string.Equals(existing, incoming, StringComparison.Ordinal);
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
