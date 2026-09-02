using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// SavedProperty 载体的注册窗口守卫与自检。版本差异(0.107.1 手动注入 vs 0.109+ 官方 Init 收录)
/// 收口在 <c>.Legacy.cs</c> / <c>.Official.cs</c> 两个分部文件里,本文件只写与版本无关的流程。
/// </summary>
internal static partial class HextechSavedPropertyBootstrap
{
	private const BindingFlags SavedPropertyFlags =
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	/// <summary>
	/// 注册窗口关闭后(wire 布局已冻结)再登记的载体必须已经在官方缓存里,否则拒绝——
	/// 允许它会让两端的 net-id 布局分叉。窗口仍开着时直接放行。
	/// </summary>
	internal static void EnsureModelTypeRegistrationAllowed(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		if (!IsRegistrationWindowClosed())
		{
			return;
		}

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
		EnsureModelTypeRegistrationAllowed(type);
		InjectModelTypeCore(type);
	}

	internal static void InjectCaches()
	{
		InjectCachesCore();
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
		string message =
			$"[{ModInfo.Id}] SavedProperty 载体 {type.FullName} 在 {RegistrationFreezePointName} 之后注册，"
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
}
