using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunesSponsorPack;

internal static class EnchantmentCompositionAdapter
{
	private const string MultiEnchantmentAssemblyName = "MultiEnchantmentMod";
	private const string MultiEnchantmentApiTypeName = "MultiEnchantmentMod.Api.MultiEnchantmentApi";
	private const string LegacyMultiEnchantmentSupportTypeName = "MultiEnchantmentMod.MultiEnchantmentSupport";
	private const string ExternalCompositeTypeName = "RepeatableEnchantments.RepeatableCompositeEnchantment";
	private static readonly object CacheLock = new();
	private static readonly Dictionary<Type, MethodInfo?> ExternalFindMethods = [];
	private static readonly Dictionary<Type, MethodInfo?> ExternalContainsMethods = [];
	private static readonly HashSet<Type> LoggedInvocationFailures = [];
	private static Func<CardModel, Type, EnchantmentModel?>? _multiEnchantmentFind;
	private static Func<CardModel, IEnumerable<EnchantmentModel>>? _legacyMultiEnchantmentEnumerate;
	private static bool _multiEnchantmentProviderResolved;
	private static bool _loggedMultiEnchantmentInvocationFailure;

	internal static bool Contains(CardModel? card, Type enchantmentType)
	{
		return Find(card, enchantmentType) != null;
	}

	internal static EnchantmentModel? Find(CardModel? card, Type enchantmentType)
	{
		if (card == null)
		{
			return null;
		}

		EnchantmentModel? external = FindViaMultiEnchantmentMod(card, enchantmentType);
		return external ?? Find(card.Enchantment, enchantmentType);
	}

	internal static bool Contains(EnchantmentModel? enchantment, Type enchantmentType)
	{
		if (enchantment == null)
		{
			return false;
		}

		if (enchantment.GetType() == enchantmentType)
		{
			return true;
		}

		if (enchantment is SponsorCompositeEnchantment sponsorComposite)
		{
			return sponsorComposite.ContainsEnchantmentType(enchantmentType);
		}

		Type compositeType = enchantment.GetType();
		if (!IsExternalComposite(compositeType))
		{
			return false;
		}

		MethodInfo? containsMethod = GetExternalContainsMethod(compositeType);
		if (containsMethod == null)
		{
			return Find(enchantment, enchantmentType) != null;
		}

		try
		{
			return containsMethod.Invoke(enchantment, [ enchantmentType ]) is true;
		}
		catch (Exception ex)
		{
			LogInvocationFailure(compositeType, ex);
			return false;
		}
	}

	internal static EnchantmentModel? Find(EnchantmentModel? enchantment, Type enchantmentType)
	{
		if (enchantment == null)
		{
			return null;
		}

		if (enchantment.GetType() == enchantmentType)
		{
			return enchantment;
		}

		if (enchantment is SponsorCompositeEnchantment sponsorComposite)
		{
			return sponsorComposite.FindEnchantment(enchantmentType);
		}

		Type compositeType = enchantment.GetType();
		if (!IsExternalComposite(compositeType))
		{
			return null;
		}

		MethodInfo? findMethod = GetExternalFindMethod(compositeType);
		if (findMethod == null)
		{
			return null;
		}

		try
		{
			return findMethod.Invoke(enchantment, [ enchantmentType ]) as EnchantmentModel;
		}
		catch (Exception ex)
		{
			LogInvocationFailure(compositeType, ex);
			return null;
		}
	}

	private static bool IsExternalComposite(Type type)
	{
		return string.Equals(type.FullName, ExternalCompositeTypeName, StringComparison.Ordinal);
	}

	private static EnchantmentModel? FindViaMultiEnchantmentMod(CardModel card, Type enchantmentType)
	{
		try
		{
			ResolveMultiEnchantmentProvider();
			if (_multiEnchantmentFind != null)
			{
				return _multiEnchantmentFind(card, enchantmentType);
			}

			return _legacyMultiEnchantmentEnumerate?.Invoke(card)
				.FirstOrDefault(enchantment => enchantmentType.IsInstanceOfType(enchantment));
		}
		catch (Exception ex)
		{
			lock (CacheLock)
			{
				_multiEnchantmentProviderResolved = true;
			}
			LogMultiEnchantmentInvocationFailure(ex);
			return null;
		}
	}

	private static void ResolveMultiEnchantmentProvider()
	{
		if (_multiEnchantmentProviderResolved)
		{
			return;
		}

		lock (CacheLock)
		{
			if (_multiEnchantmentProviderResolved)
			{
				return;
			}

			Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(static candidate =>
				string.Equals(candidate.GetName().Name, MultiEnchantmentAssemblyName, StringComparison.Ordinal));
			if (assembly == null)
			{
				// 模组初始化顺序不固定；程序集尚未加载时不能缓存“未安装”。
				return;
			}

			Type? apiType = assembly.GetType(MultiEnchantmentApiTypeName, throwOnError: false);
			MethodInfo? findMethod = apiType?.GetMethod(
				"GetEnchantment",
				BindingFlags.Static | BindingFlags.Public,
				null,
				[ typeof(CardModel), typeof(Type) ],
				null);
			if (findMethod?.ReturnType == typeof(EnchantmentModel))
			{
				_multiEnchantmentFind = findMethod.CreateDelegate<Func<CardModel, Type, EnchantmentModel?>>();
			}
			else
			{
				Type? supportType = assembly.GetType(LegacyMultiEnchantmentSupportTypeName, throwOnError: false);
				MethodInfo? enumerateMethod = supportType?.GetMethod(
					"GetEnchantments",
					BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
					null,
					[ typeof(CardModel) ],
					null);
				if (enumerateMethod != null
					&& typeof(IEnumerable<EnchantmentModel>).IsAssignableFrom(enumerateMethod.ReturnType))
				{
					_legacyMultiEnchantmentEnumerate = enumerateMethod.CreateDelegate<Func<CardModel, IEnumerable<EnchantmentModel>>>();
				}
			}

			_multiEnchantmentProviderResolved = true;
		}
	}

	private static MethodInfo? GetExternalFindMethod(Type compositeType)
	{
		lock (CacheLock)
		{
			if (ExternalFindMethods.TryGetValue(compositeType, out MethodInfo? cached))
			{
				return cached;
			}

			MethodInfo? method = compositeType.GetMethod(
				"FindEnchantment",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				[ typeof(Type) ],
				null);
			ExternalFindMethods[compositeType] = method;
			return method;
		}
	}

	private static MethodInfo? GetExternalContainsMethod(Type compositeType)
	{
		lock (CacheLock)
		{
			if (ExternalContainsMethods.TryGetValue(compositeType, out MethodInfo? cached))
			{
				return cached;
			}

			MethodInfo? method = compositeType.GetMethod(
				"ContainsEnchantmentType",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				[ typeof(Type) ],
				null);
			ExternalContainsMethods[compositeType] = method;
			return method;
		}
	}

	private static void LogInvocationFailure(Type compositeType, Exception ex)
	{
		lock (CacheLock)
		{
			if (LoggedInvocationFailures.Add(compositeType))
			{
				Log.Warn($"[{ModInfo.Id}] Failed to inspect external repeatable enchantment composite {compositeType.FullName}: {ex.GetType().Name}: {ex.Message}", 2);
			}
		}
	}

	private static void LogMultiEnchantmentInvocationFailure(Exception ex)
	{
		lock (CacheLock)
		{
			if (_loggedMultiEnchantmentInvocationFailure)
			{
				return;
			}

			_loggedMultiEnchantmentInvocationFailure = true;
			Log.Warn($"[{ModInfo.Id}] Failed to query MultiEnchantmentMod: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}
}
