using System.Reflection;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void ActualDamageHookCannotSuppressOutOfCombatCalls()
	{
		MethodInfo prefix = HextechPatcher.FindPatchMethod(typeof(HextechCombatHooks), "DamageCommandPatch", "Prefix")
			?? throw new InvalidOperationException("Actual damage command prefix is missing.");

		Equal(typeof(void), prefix.ReturnType, "actual damage prefix return type");
		ParameterInfo[] parameters = prefix.GetParameters();
		Equal(1, parameters.Length, "actual damage prefix parameter count");
		Expect(parameters[0].IsOut && parameters[0].ParameterType == typeof(long).MakeByRefType(),
			"Actual damage prefix should only allocate command state and must not receive targets or replace the result.");
	}

	private static void HookReflectionRequiresExactSignatures()
	{
		MethodInfo exact = HextechHookReflection.RequireMethod(
			typeof(ReflectionSignatureFixture),
			nameof(ReflectionSignatureFixture.Target),
			BindingFlags.NonPublic | BindingFlags.Static,
			typeof(string));
		Equal(typeof(string), exact.GetParameters()[0].ParameterType, "exact reflection parameter");

		ExpectThrows<InvalidOperationException>(
			() => HextechHookReflection.RequireMethod(
				typeof(ReflectionSignatureFixture),
				nameof(ReflectionSignatureFixture.Target),
				BindingFlags.NonPublic | BindingFlags.Static,
				typeof(int)),
			"A same-name, same-arity method with a different signature must not be selected.");
	}

	private static class ReflectionSignatureFixture
	{
		internal static void Target(string value)
		{
			_ = value;
		}
	}
}
