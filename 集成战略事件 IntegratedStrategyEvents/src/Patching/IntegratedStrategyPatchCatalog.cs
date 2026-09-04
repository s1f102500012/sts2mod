using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;

namespace IntegratedStrategyEvents;

internal static class IntegratedStrategyPatchCatalog
{
	internal const BindingFlags Methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	internal static IEnumerable<Type> PatchTypes(Assembly assembly) => assembly.GetTypes()
		.Where(type => type.IsDefined(typeof(HarmonyPatch), false) || type.IsDefined(typeof(IntegratedStrategyPatchAttribute), false))
		.OrderBy(type => type.FullName, StringComparer.Ordinal);

	internal static IEnumerable<(Type Type, MethodBase? Method)> Targets(Type patchType)
	{
		HarmonyMethod target = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchType));
		if (target.declaringType == null || string.IsNullOrEmpty(target.methodName))
			throw new InvalidOperationException($"{patchType.FullName} has no declarative target.");
		Type[] additional = patchType.GetCustomAttribute<IntegratedStrategyPatchAttribute>()?.AdditionalTargets ?? [];
		foreach (Type type in new[] { target.declaringType }.Concat(additional).Distinct())
		{
			MethodBase? method = target.methodType switch
			{
				MethodType.Getter => AccessTools.PropertyGetter(type, target.methodName),
				MethodType.Setter => AccessTools.PropertySetter(type, target.methodName),
				_ => AccessTools.Method(type, target.methodName, target.argumentTypes)
			};
			yield return (type, method);
		}
	}

	internal static MethodInfo? PatchMethod(Type type, string kind) => type.GetMethods(Methods).SingleOrDefault(method =>
		method.Name == kind || method.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == "Harmony" + kind));

	internal static HarmonyMethod? Descriptor(Type type, string kind)
	{
		MethodInfo? method = PatchMethod(type, kind);
		if (method == null) return null;
		HarmonyMethod descriptor = new(method);
		if (descriptor.priority < 0) descriptor.priority = Priority.Normal;
		return descriptor;
	}

	internal static string TargetKey(MethodBase method) => $"{method.DeclaringType?.FullName}::{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName))})";

	// async 的门面 IL 通常不变，必须把 MoveNext 一起冻结。
	internal static string IlHash(MethodBase method)
	{
		byte[] body = method.GetMethodBody()?.GetILAsByteArray() ?? [];
		Type? stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
			?? method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
		byte[] continuation = stateMachine?.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
			?.GetMethodBody()?.GetILAsByteArray() ?? [];
		return Convert.ToHexString(SHA256.HashData([.. body, .. continuation])).ToLowerInvariant();
	}

	internal static IEnumerable<string> Describe(Assembly assembly)
	{
		foreach (Type type in PatchTypes(assembly))
		foreach ((_, MethodBase? method) in Targets(type))
		{
			if (method == null) throw new MissingMethodException(type.FullName);
			foreach (string kind in new[] { "Prefix", "Postfix", "Finalizer", "Transpiler" })
			{
				HarmonyMethod? patch = Descriptor(type, kind);
				if (patch == null) continue;
				yield return $"{TargetKey(method)}|{kind}|{type.FullName}.{patch.method.Name}|priority={patch.priority}|before={string.Join(",", patch.before ?? [])}|after={string.Join(",", patch.after ?? [])}|il={IlHash(method)}";
			}
		}
	}
}
