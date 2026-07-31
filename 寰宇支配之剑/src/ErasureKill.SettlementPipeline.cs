using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private static MethodInfo? OriginalCombatSettlementInvoker;

	private static void PatchCanonicalSettlementEntry(Harmony harmony)
	{
		MethodInfo original = GetEndCombatMethods()
			.Single(IsSettlementEndMethod);
		// The settlement leaf can use an internal combat-epoch type, so the
		// reverse-patch stand-in must have its exact runtime signature.
		Type[] originalParameters = original.GetParameters()
			.Select(parameter => parameter.ParameterType)
			.ToArray();
		Type[] standinParameters =
			[typeof(CombatManager), .. originalParameters];

		AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
			new AssemblyName(
				"UniversalDominionSword.CanonicalCombatPrimitives"),
			AssemblyBuilderAccess.Run);
		ModuleBuilder module = assembly.DefineDynamicModule(
			"CanonicalCombatPrimitives");
		TypeBuilder type = module.DefineType(
			"UniversalDominionSword.CanonicalCombatSettlement",
			TypeAttributes.Class
				| TypeAttributes.Abstract
				| TypeAttributes.Sealed
				| TypeAttributes.NotPublic);
		MethodBuilder standinBuilder = type.DefineMethod(
			"InvokeOriginal",
			MethodAttributes.Public | MethodAttributes.Static,
			typeof(Task),
			standinParameters);
		ILGenerator il = standinBuilder.GetILGenerator();
		il.Emit(
			OpCodes.Ldstr,
			"The canonical combat settlement primitive was not initialized.");
		il.Emit(
			OpCodes.Newobj,
			typeof(NotSupportedException).GetConstructor([typeof(string)])
				?? throw new MissingMethodException(
					typeof(NotSupportedException).FullName,
					".ctor(string)"));
		il.Emit(OpCodes.Throw);

		Type standinType = type.CreateType()
			?? throw new TypeLoadException(
				"The canonical combat settlement stand-in could not be created.");
		MethodInfo standin = standinType.GetMethod(
				"InvokeOriginal",
				BindingFlags.Public | BindingFlags.Static)
			?? throw new MissingMethodException(
				standinType.FullName,
				"InvokeOriginal");
		MethodInfo? patched = harmony.CreateReversePatcher(
				original,
				new HarmonyMethod(standin))
			.Patch(HarmonyReversePatchType.Original);
		OriginalCombatSettlementInvoker = patched ?? standin;
	}

	private static Task InvokeOriginalCombatSettlement(
		CombatManager manager,
		object? turnState)
	{
		MethodInfo invoker = OriginalCombatSettlementInvoker
			?? throw new InvalidOperationException(
				"The canonical combat settlement primitive is unavailable.");
		ParameterInfo[] parameters = invoker.GetParameters();
		object?[] arguments;
		if (parameters.Length == 1)
		{
			arguments = [manager];
		}
		else if (parameters.Length == 2 && turnState != null)
		{
			arguments = [manager, turnState];
		}
		else
		{
			throw new InvalidOperationException(
				"The active combat epoch does not match the settlement primitive.");
		}

		try
		{
			return invoker.Invoke(null, arguments) as Task
				?? throw new InvalidOperationException(
					"The canonical combat settlement returned no task.");
		}
		catch (TargetInvocationException exception)
			when (exception.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			throw;
		}
	}
}
