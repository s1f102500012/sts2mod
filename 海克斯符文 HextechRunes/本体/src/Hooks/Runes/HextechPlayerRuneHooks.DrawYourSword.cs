using HarmonyLib;

namespace HextechRunes;

internal static partial class HextechPlayerRuneHooks
{

	internal static IReadOnlyList<MethodInfo> FindLoadedOrbEvokeMethods()
	{
		Assembly coreAssembly = typeof(OrbModel).Assembly;
		string? coreAssemblyName = coreAssembly.GetName().Name;
		HashSet<MethodInfo> methods =
		[
			typeof(OrbModel).GetMethod(
				nameof(OrbModel.Evoke),
				BindingFlags.Instance | BindingFlags.Public,
				binder: null,
				types: [typeof(PlayerChoiceContext)],
				modifiers: null)
				?? throw new MissingMethodException(typeof(OrbModel).FullName, nameof(OrbModel.Evoke))
		];

		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic || !CanContainOrbModels(assembly, coreAssembly, coreAssemblyName))
			{
				continue;
			}

			foreach (Type type in GetLoadableTypes(assembly, coreAssembly))
			{
				if (type == typeof(OrbModel) || !typeof(OrbModel).IsAssignableFrom(type))
				{
					continue;
				}

				MethodInfo? evoke = type.GetMethod(
					nameof(OrbModel.Evoke),
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
					binder: null,
					types: [typeof(PlayerChoiceContext)],
					modifiers: null);
				if (evoke is { IsAbstract: false } && evoke.ReturnType == typeof(Task<IEnumerable<Creature>>))
				{
					methods.Add(evoke);
				}
			}
		}

		return methods
			.OrderBy(static method => method.DeclaringType?.Assembly.FullName, StringComparer.Ordinal)
			.ThenBy(static method => method.DeclaringType?.FullName, StringComparer.Ordinal)
			.ToArray();
	}

	internal static bool CanContainOrbModels(Assembly assembly, Assembly coreAssembly, string? coreAssemblyName)
	{
		if (assembly == coreAssembly)
		{
			return true;
		}

		try
		{
			return assembly.GetReferencedAssemblies()
				.Any(reference => string.Equals(reference.Name, coreAssemblyName, StringComparison.Ordinal));
		}
		catch (Exception)
		{
			return false;
		}
	}

	internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly, Assembly coreAssembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(static type => type != null).Cast<Type>();
		}
		catch (Exception ex) when (assembly != coreAssembly)
		{
			Log.Warn($"[{ModInfo.Id}][Compat] Could not inspect external assembly {assembly.FullName} for Orb models: {ex.Message}");
			return Array.Empty<Type>();
		}
	}

	internal static bool OrbEvokePrefix(OrbModel __instance, ref Task<IEnumerable<Creature>> __result)
	{
		DrawYourSwordRune? rune = __instance.Owner?.GetRelic<DrawYourSwordRune>();
		if (rune == null || !rune.ShouldReplaceOrbEvoke(__instance))
		{
			return true;
		}

		__result = rune.ReplaceOrbEvoke();
		return false;
	}

}
