using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechSavedPropertyBootstrap
{
	private const BindingFlags SavedPropertyFlags =
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

#if STS2_109_OR_NEWER
	private static readonly FieldInfo? OfficialCacheInitializedField = TryGetField(
		typeof(SavedPropertiesTypeCache),
		"_initialized",
		BindingFlags.NonPublic | BindingFlags.Static,
		warnIfMissing: false);
#endif

	internal static void EnsureModelTypeRegistrationAllowed(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

	#if STS2_109_OR_NEWER
		if (!IsOfficialCacheInitialized())
		{
			return;
		}
	#else
		if (!HextechSavedPropertyNetIdHooks.IsCanonicalized)
		{
			return;
		}
	#endif

		PropertyInfo[] savedProperties = GetSavedProperties(type);
		if (savedProperties.Length == 0)
		{
			return;
		}

		IReadOnlyList<PropertyInfo>? cachedProperties;
		try
		{
			cachedProperties = SavedPropertiesTypeCache.GetJsonPropertiesForType(type);
		}
		catch (Exception ex)
		{
			throw CreateLateRegistrationException(type, savedProperties, ex);
		}

		if (cachedProperties == null)
		{
			throw CreateLateRegistrationException(type, savedProperties);
		}

		PropertyInfo[] missingProperties = savedProperties
			.Where(property => !ContainsProperty(cachedProperties, property))
			.ToArray();
		if (missingProperties.Length > 0)
		{
			throw CreateLateRegistrationException(type, missingProperties);
		}
	}

	internal static void InjectModelType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

#if STS2_109_OR_NEWER
		// 0.109.0 的 Init 会从 ModelDb.All 统一排序、编号并散列 SavedProperty。Init 前调用
		// CacheSavedPropertiesForTypeDebug 会提前写表且绕过该散列；Init 后追加则会破坏已经发布的 wire 布局。
		// 因此这里只验证官方缓存是否已覆盖该载体，绝不调用 Debug 注入入口。
		EnsureModelTypeRegistrationAllowed(type);
		return;
#else
		EnsureModelTypeRegistrationAllowed(type);
		if (HextechSavedPropertyNetIdHooks.IsCanonicalized)
		{
			return;
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(type);
#endif
	}

	internal static void InjectCaches()
	{
#if STS2_109_OR_NEWER
		// 0.109.0:注入与位宽兜底全部由游戏侧 Init() 承担;自检推迟到 ExecuteEssential 后
		// (此刻表尚未填充,现跑必误报全量),见 HextechSavedPropertyNetIdHooks 的 0.109 分支。
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SavedProperty 注入跳过:0.109+ 由 ModelIdSerializationCache.Init 自动收录 ModelDb 载体。");
#else
		foreach (Type type in HextechModelTypeIdentity.Distinct(HextechCatalog.GetAllCustomRelicTypes()))
		{
			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechMayhemModifier));
		foreach (Type type in HextechCustomModelRegistry.AllCustomModifierTypes)
		{
			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechBurnPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechNextTurnDamagePower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechGalvanicPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryDexterityPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryStrengthLossPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryDexterityLossPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechLethalTempoTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechBloodPactTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechPowerShieldTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechAttackReplayPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechPlayerSlowPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporarySlowPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechOceanDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechInfernalDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechMountainDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechChemtechDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechCloudDragonSoulPower));
		WarnOnUninjectedSavedPropertyCarriers();
		EnsureSavedPropertyNetIdBitSize();
#endif
	}

	// 启动自检同时核对全局 net-id 名字表和每个载体自己的 PropertyInfo 缓存。前者决定 wire 布局，
	// 后者决定保存/同步时实际枚举哪些属性；只查名字会漏掉“同名属性已存在、载体本身未缓存”的静默丢字段。
	internal static void WarnOnUninjectedSavedPropertyCarriers()
	{
		try
		{
			HashSet<string>? registeredNames = TryGetRegisteredSavedPropertyNames();
			if (registeredNames == null)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] SavedProperty net-id 名字表自检跳过:取不到名字表;继续核对各载体的 per-type cache。");
			}

			System.Type abstractModelType = typeof(MegaCrit.Sts2.Core.Models.AbstractModel);
			HashSet<(System.Type CarrierType, string PropertyName)> warned = [];

			foreach (Assembly assembly in GetAssembliesToAudit())
			{
				foreach (System.Type type in GetLoadableTypes(assembly))
				{
					if (type.IsAbstract || !type.IsClass || !abstractModelType.IsAssignableFrom(type))
					{
						continue;
					}

					IReadOnlyList<PropertyInfo>? cachedProperties = TryGetCachedPropertiesForType(type);
					foreach (PropertyInfo property in GetSavedProperties(type))
					{
						bool missingGlobalName = registeredNames != null
							&& !registeredNames.Contains(property.Name);
						bool missingCarrierProperty = cachedProperties == null
							|| !ContainsProperty(cachedProperties, property);
						if ((!missingGlobalName && !missingCarrierProperty)
							|| !warned.Add((type, property.Name)))
						{
							continue;
						}

						string missingPart = missingGlobalName && missingCarrierProperty
							? "未进 net-id 名字表及该载体的 per-type cache"
							: missingGlobalName
								? "未进 net-id 名字表"
								: "未进该载体的 per-type cache";
						Log.Warn($"[{ModInfo.Id}][Mayhem] SavedProperty 注入自检:载体 {type.FullName} 的 [SavedProperty] \"{property.Name}\" {missingPart};联机(反)序列化可能抛 \"could not be mapped\" 或静默漏字段。请在模型注册窗口内显式登记该 SavedProperty 载体。");
					}
				}
			}
		}
		catch (System.Exception ex)
		{
			// 纯诊断:任何反射异常都不得影响模组加载。
			Log.Warn($"[{ModInfo.Id}][Mayhem] SavedProperty 注入自检跳过: {ex.Message}");
		}
	}

	private static PropertyInfo[] GetSavedProperties(System.Type type)
	{
		return type
			.GetProperties(SavedPropertyFlags)
			.Where(static property => property
				.GetCustomAttributes(inherit: true)
				.Any(static attr => attr.GetType().Name == "SavedPropertyAttribute"))
			.ToArray();
	}

	private static IReadOnlyList<PropertyInfo>? TryGetCachedPropertiesForType(System.Type type)
	{
		try
		{
			return SavedPropertiesTypeCache.GetJsonPropertiesForType(type);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool ContainsProperty(
		IReadOnlyList<PropertyInfo> cachedProperties,
		PropertyInfo expected)
	{
		return cachedProperties.Any(property =>
			property.Equals(expected)
			|| (property.DeclaringType == expected.DeclaringType
				&& string.Equals(property.Name, expected.Name, StringComparison.Ordinal)
				&& property.PropertyType == expected.PropertyType));
	}

	private static bool IsOfficialCacheInitialized()
	{
	#if STS2_109_OR_NEWER
		if (OfficialCacheInitializedField?.GetValue(null) is bool initialized)
		{
			return initialized;
		}

		throw new InvalidOperationException(
			$"[{ModInfo.Id}] 无法读取 ModelIdSerializationCache._initialized；为避免污染 SavedProperty net-id 表，已拒绝外部模型注册。");
	#else
		return false;
	#endif
	}

	private static InvalidOperationException CreateLateRegistrationException(
		System.Type type,
		IReadOnlyList<PropertyInfo> missingProperties,
		Exception? innerException = null)
	{
		string propertyNames = string.Join(
			", ",
			missingProperties
				.Select(static property => property.Name)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(static name => name, StringComparer.Ordinal));
		string freezePoint =
	#if STS2_109_OR_NEWER
			"ModelIdSerializationCache.Init";
	#else
			"SavedProperty net-id 规范化";
	#endif
		string message =
			$"[{ModInfo.Id}] SavedProperty 载体 {type.FullName} 在 {freezePoint} 之后注册，"
			+ $"但 per-type cache 缺少属性 [{propertyNames}]。为保持联机 net-id 布局不变，已拒绝延迟注册。";
		return new InvalidOperationException(message, innerException);
	}

	// 自检范围 = 本程序集 + 所有已加载且引用了本程序集的包(拓展包/二创包的载体也要能被抓到)。
	private static IEnumerable<Assembly> GetAssembliesToAudit()
	{
		Assembly self = Assembly.GetExecutingAssembly();
		yield return self;

		string? selfName = self.GetName().Name;
		foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly == self || assembly.IsDynamic)
			{
				continue;
			}

			bool referencesSelf = false;
			try
			{
				referencesSelf = assembly
					.GetReferencedAssemblies()
					.Any(reference => string.Equals(reference.Name, selfName, StringComparison.Ordinal));
			}
			catch (System.Exception)
			{
				// 个别程序集的引用表读不出来就跳过,不影响其余扫描。
			}

			if (referencesSelf)
			{
				yield return assembly;
			}
		}
	}

	private static System.Type[] GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(static type => type != null).Cast<System.Type>().ToArray();
		}
	}

	private static HashSet<string>? TryGetRegisteredSavedPropertyNames()
	{
		FieldInfo? mapField = TryGetField(
			typeof(SavedPropertiesTypeCache),
			"_netIdToPropertyNameMap",
			BindingFlags.NonPublic | BindingFlags.Static);
		if (mapField?.GetValue(null) is not System.Collections.IEnumerable names)
		{
			return null;
		}

		HashSet<string> result = new(StringComparer.Ordinal);
		foreach (object? name in names)
		{
			if (name is string text)
			{
				result.Add(text);
			}
		}

		return result;
	}

#if !STS2_109_OR_NEWER
	private static void EnsureSavedPropertyNetIdBitSize()
	{
		// 兜底:按与游戏 / RitsuLib 一致的公式 CeilToInt(Log2(count)) 把位宽抬到能容纳当前属性数。
		// 联机一致性的权威设置由 HextechSavedPropertyNetIdHooks 在规范化后统一完成;此处仅保证即便该
		// 后缀钩子未能安装,本模组单独联机时位宽也够用。不再使用旧的固定下限 16——它与原版 / RitsuLib 的
		// 公式不一致,会让一端是 16、另一端是 CeilToInt(Log2(count)),造成 net-id 位宽错位而断连。
		const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

		FieldInfo? mapField = TryGetField(typeof(SavedPropertiesTypeCache), "_netIdToPropertyNameMap", flags);
		int propertyNameCount = (mapField?.GetValue(null) as System.Collections.ICollection)?.Count ?? 0;
		int targetBitSize = HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(propertyNameCount);
		int currentBitSize = SavedPropertiesTypeCache.NetIdBitSize;
		if (currentBitSize >= targetBitSize)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SavedPropertiesTypeCache NetIdBitSize unchanged: bitSize={currentBitSize} propertyNames={propertyNameCount}");
			return;
		}

		FieldInfo? backingField = TryGetField(typeof(SavedPropertiesTypeCache), "<NetIdBitSize>k__BackingField", flags);
		if (backingField == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SavedPropertiesTypeCache NetIdBitSize backing field not found; custom saved properties may desync in multiplayer.");
			return;
		}

		backingField.SetValue(null, targetBitSize);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SavedPropertiesTypeCache NetIdBitSize updated: old={currentBitSize} new={targetBitSize} propertyNames={propertyNameCount}");
	}
#endif
}
