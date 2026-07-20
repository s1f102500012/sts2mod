using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechSavedPropertyBootstrap
{
	internal static void InjectModelType(Type type)
	{
		if (HextechSavedPropertyNetIdHooks.IsCanonicalized)
		{
			// 规范化是一次性的(ExecuteEssential 后缀):此后注入的属性名按加载顺序追加、绕过规范排序,
			// 两端顺序可能错位 → 复刻 1014。二创/外部包必须在游戏启动初始化阶段注册,勿延迟到 run 前。
			Log.Warn($"[{ModInfo.Id}][Mayhem] SavedProperty 载体 {type.FullName} 在 net-id 规范化之后才注入:其属性按加载顺序追加,联机可能 1014/ModMismatch。请提前到启动初始化阶段注册。");
		}

#if STS2_109_OR_NEWER
		// 0.109.0 起游戏在 OneTimeInitialization.ExecuteEssential 里由 ModelIdSerializationCache.Init()
		// 从 ModelDb.All 自动收录全部载体并做确定性排序;Init 前无需(也不能,守卫会抛)手动注入。
		// Init 之后的补注入走官方 CacheSavedPropertiesForTypeDebug(幂等:已缓存类型直接返回,
		// 未缓存则追加属性 net-id——与 0.108 延迟注入同样是"追加尾部"语义,时序告警照旧适用)。
		try
		{
			SavedPropertiesTypeCache.CacheSavedPropertiesForTypeDebug(type);
		}
		catch (InvalidOperationException)
		{
			// Init 尚未运行:类型此刻已在 ModelDb,启动流程稍后会统一收录。
		}
#else
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
		foreach (Type type in HextechCustomModelRegistry.CustomRarityModifierTypes)
		{
			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechBurnPower));
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

	// R3 启动自检:扫本程序集所有 AbstractModel 载体,凡带 [SavedProperty] 却没进 net-id 表的属性名都告警。
	// 与游戏一致地按【属性名】核对(SavedPropertiesTypeCache 也是按名注册/查询):缺名会让联机(反)序列化
	// 在 GetNetIdForPropertyName 抛 "could not be mapped" → 概率性 1014/ModMismatch。此处只 Log.Warn、绝不抛,
	// 把"漏登记 rune / 漏加 Power 到手写清单"这类隐患从线上崩提前到启动日志。时机:所有 InjectTypeIntoCache 之后、
	// 后续规范化只重排不增删名字,故此刻名字集合已是最终集合。
	internal static void WarnOnUninjectedSavedPropertyCarriers()
	{
		try
		{
			HashSet<string>? registeredNames = TryGetRegisteredSavedPropertyNames();
			if (registeredNames == null)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] SavedProperty 注入自检跳过:取不到 net-id 名字表。");
				return;
			}

			System.Type abstractModelType = typeof(MegaCrit.Sts2.Core.Models.AbstractModel);
			HashSet<string> warned = new(StringComparer.Ordinal);
			const BindingFlags propertyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			foreach (Assembly assembly in GetAssembliesToAudit())
			{
				foreach (System.Type type in GetLoadableTypes(assembly))
				{
					if (type.IsAbstract || !type.IsClass || !abstractModelType.IsAssignableFrom(type))
					{
						continue;
					}

					foreach (PropertyInfo property in type.GetProperties(propertyFlags))
					{
						bool isSavedProperty = property
							.GetCustomAttributes(inherit: true)
							.Any(static attr => attr.GetType().Name == "SavedPropertyAttribute");
						if (!isSavedProperty || registeredNames.Contains(property.Name) || !warned.Add(property.Name))
						{
							continue;
						}

						Log.Warn($"[{ModInfo.Id}][Mayhem] SavedProperty 注入自检:载体 {type.FullName} 的 [SavedProperty] \"{property.Name}\" 未进 net-id 表;联机(反)序列化会抛 \"could not be mapped\" 致 1014/ModMismatch。请把该类型登记进 catalog,或加入 InjectCaches 的注入清单。");
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
