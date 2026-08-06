using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechEnemyPowerScalingHooks
{
#if STS2_105_OR_NEWER
	private static IEnumerable<MethodInfo> ResolveGetScaledAmountForMultiplayerTargets()
	{
		List<MethodInfo> targets = new();
		foreach (Type powerType in GetPowerTypesWithScalingOverride())
		{
			MethodInfo? method = TryGetMethod(
				powerType,
				nameof(PowerModel.GetScaledAmountForMultiplayer),
				BindingFlags.Public | BindingFlags.Instance,
				warnIfMissing: false,
				typeof(HextechCombatState),
				typeof(Creature),
				typeof(decimal),
				typeof(Creature),
				typeof(CardModel));
			if (method == null)
			{
				continue;
			}

			Type declaringType = method.DeclaringType ?? typeof(PowerModel);
			method = TryGetMethod(
				declaringType,
				nameof(PowerModel.GetScaledAmountForMultiplayer),
				BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
				warnIfMissing: false,
				typeof(HextechCombatState),
				typeof(Creature),
				typeof(decimal),
				typeof(Creature),
				typeof(CardModel));
			if (method == null)
			{
				continue;
			}

			if (!ContainsMethod(targets, method))
			{
				targets.Add(method);
			}
		}

		if (targets.Count == 0)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem][Compat] Enemy power multiplayer scaling hook skipped: GetScaledAmountForMultiplayer targets not found in this runtime.");
		}

		return targets;
	}

	private static IEnumerable<Type> GetPowerTypesWithScalingOverride()
	{
		yield return typeof(ArtifactPower);
		yield return typeof(SlipperyPower);
		yield return typeof(HardenedShellPower);
		yield return typeof(RegenPower);
		yield return typeof(PlatingPower);
		yield return typeof(ReflectPower);
		yield return typeof(SkittishPower);
	}

	private static bool ContainsMethod(IEnumerable<MethodInfo> methods, MethodInfo candidate)
	{
		foreach (MethodInfo method in methods)
		{
			if (method.Module == candidate.Module && method.MetadataToken == candidate.MetadataToken)
			{
				return true;
			}
		}

		return false;
	}
#endif
}
