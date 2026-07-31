using MegaCrit.Sts2.Core.Hooks;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechEnemyPowerScalingHooks
{
	private static MethodInfo? TryResolveModifyPowerAmountGivenTarget()
	{
#if STS2_107_OR_NEWER
		return TryGetMethod(
			typeof(Hook),
			nameof(Hook.ModifyPowerAmountGiven),
			BindingFlags.Public | BindingFlags.Static,
			warnIfMissing: false,
			typeof(ICombatState),
			typeof(PowerModel),
			typeof(Creature),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel),
			typeof(IEnumerable<AbstractModel>).MakeByRefType());
#else
		MethodInfo? reflectedMethod = TryGetMethod(
			typeof(MultiplayerScalingModel),
			nameof(MultiplayerScalingModel.ModifyPowerAmountGiven),
			BindingFlags.Public | BindingFlags.Instance,
			warnIfMissing: false,
			typeof(PowerModel),
			typeof(Creature),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel));
		if (reflectedMethod == null)
		{
			return null;
		}

		if (reflectedMethod.DeclaringType == typeof(MultiplayerScalingModel)
			&& reflectedMethod.GetMethodBody() != null)
		{
			return reflectedMethod;
		}

		MethodInfo baseDefinition = reflectedMethod.GetBaseDefinition();
		if (baseDefinition.GetMethodBody() != null)
		{
			return baseDefinition;
		}

		Type declaringType = reflectedMethod.DeclaringType ?? typeof(AbstractModel);
		return TryGetMethod(
			declaringType,
			nameof(AbstractModel.ModifyPowerAmountGiven),
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
			warnIfMissing: false,
			typeof(PowerModel),
			typeof(Creature),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel));
#endif
	}

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
