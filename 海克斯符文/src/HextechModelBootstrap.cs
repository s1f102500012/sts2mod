using System.Reflection;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunes;

internal static class HextechModelBootstrap
{
	private static readonly MethodInfo AddModelToPoolMethod = typeof(ModHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)
		.Single(method => method.Name == "AddModelToPool" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2);

	public static void Install()
	{
		PreloadDependencyAssemblies();
		InjectSavedPropertyCaches();
		RegisterModels();
	}

	private static void InjectSavedPropertyCaches()
	{
		foreach (Type type in ModInfo.GetAllRuneTypes())
		{
			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechMayhemModifier));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechBurnPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryDexterityPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryStrengthLossPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryDexterityLossPower));
	}

	private static void RegisterModels()
	{
		foreach (Type runeType in ModInfo.GetAllRuneTypes())
		{
			AddModelToPoolMethod.MakeGenericMethod(typeof(SharedRelicPool), runeType).Invoke(null, null);
		}
	}

	private static void PreloadDependencyAssemblies()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string? modDirectory = Path.GetDirectoryName(assembly.Location);
		if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
		{
			return;
		}

		string selfPath = assembly.Location;
		AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(assembly) ?? AssemblyLoadContext.Default;
		foreach (string dllPath in Directory.GetFiles(modDirectory, "*.dll"))
		{
			if (string.Equals(dllPath, selfPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			loadContext.LoadFromAssemblyPath(dllPath);
		}
	}
}
