using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents;

internal static class IntegratedStrategyModelIdSerializationWarningHooks
{
	private const string Prefix = "Two AbstractModels ";
	private static readonly string Suffix = $" from mod {ModInfo.ModId} share an ID! This might break multiplayer.";
	private static bool _installed;
	private static bool _loggedSuppression;

	public static void Install(Harmony harmony)
	{
		if (_installed)
		{
			return;
		}

		MethodInfo? warnMethod = AccessTools.Method(
			typeof(Log),
			nameof(Log.Warn),
			[typeof(string), typeof(int)]);
		if (warnMethod == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not install ModelIdSerializationCache warning filter.");
			return;
		}

		harmony.Patch(
			warnMethod,
			prefix: new HarmonyMethod(typeof(IntegratedStrategyModelIdSerializationWarningHooks), nameof(LogWarnPrefix)));
		_installed = true;
	}

	private static bool LogWarnPrefix(string text)
	{
		if (!IsFalsePositiveSelfCompareWarning(text, out string? typeFullName))
		{
			return true;
		}

		if (HasActualDuplicateModelType(typeFullName!))
		{
			return true;
		}

		if (!_loggedSuppression)
		{
			_loggedSuppression = true;
			Log.Info($"{ModInfo.LogPrefix}[MultiplayerCompat] Suppressed false-positive ModelIdSerializationCache self-compare warning for {typeFullName}.");
		}

		return false;
	}

	private static bool IsFalsePositiveSelfCompareWarning(string? text, out string? typeFullName)
	{
		typeFullName = null;
		if (string.IsNullOrEmpty(text)
			|| !text.StartsWith(Prefix, StringComparison.Ordinal)
			|| !text.EndsWith(Suffix, StringComparison.Ordinal))
		{
			return false;
		}

		string body = text.Substring(Prefix.Length, text.Length - Prefix.Length - Suffix.Length);
		string[] parts = body.Split(" and ", StringSplitOptions.None);
		if (parts.Length != 2 || !string.Equals(parts[0], parts[1], StringComparison.Ordinal))
		{
			return false;
		}

		typeFullName = parts[0];
		return !string.IsNullOrWhiteSpace(typeFullName);
	}

	private static bool HasActualDuplicateModelType(string typeFullName)
	{
		int count = 0;
		try
		{
			foreach (Mod mod in ModManager.Mods)
			{
				if (mod.state != ModLoadState.Loaded
					|| !string.Equals(mod.manifest?.id, ModInfo.ModId, StringComparison.Ordinal))
				{
					continue;
				}

				foreach (Type type in GetModAssemblies(mod).SelectMany(static assembly => assembly.GetTypes()))
				{
					if (!type.IsAbstract
						&& !type.IsInterface
						&& type.IsSubclassOf(typeof(AbstractModel))
						&& string.Equals(type.FullName, typeFullName, StringComparison.Ordinal)
						&& ++count > 1)
					{
						return true;
					}
				}
			}
		}
		catch
		{
			return true;
		}

		return false;
	}

	private static IEnumerable<Assembly> GetModAssemblies(Mod mod)
	{
#if STS2_109_OR_NEWER
		return mod.assemblies ?? [];
#else
		return mod.assembly == null ? [] : [mod.assembly];
#endif
	}
}
