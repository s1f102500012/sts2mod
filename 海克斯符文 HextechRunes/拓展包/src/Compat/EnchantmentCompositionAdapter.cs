using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunesSponsorPack;

internal static class EnchantmentCompositionAdapter
{
	private const string ExternalCompositeTypeName = "RepeatableEnchantments.RepeatableCompositeEnchantment";
	private static readonly object CacheLock = new();
	private static readonly Dictionary<Type, MethodInfo?> ExternalFindMethods = [];
	private static readonly Dictionary<Type, MethodInfo?> ExternalContainsMethods = [];
	private static readonly HashSet<Type> LoggedInvocationFailures = [];

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
}
