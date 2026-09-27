using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static readonly Dictionary<short, OpCode> IlOpCodes = typeof(OpCodes)
		.GetFields(BindingFlags.Public | BindingFlags.Static)
		.Select(field => (OpCode)field.GetValue(null)!)
		.ToDictionary(code => code.Value);

	// 第三方模组会批量给模型钩子打 Harmony 补丁;引用类型实参的泛型实例共享机器码,补丁替换体会把
	// 首个被补实例的类型参数写死给全部实例(WhoCarried 让五个形态符文只认同一种形态牌)。
	// 泛型模型类里声明的 Task 钩子重写因此只能转发到本类实例方法:体内不得出现类型参数、
	// 泛型方法实例或 async 状态机。
	private static void GenericModelHookOverridesDoNotUseTypeParameters()
	{
		List<string> violations = [];
		int checkedMethods = 0;
		foreach (Type type in typeof(HextechRelicBase).Assembly.GetTypes()
			.Where(type => type.IsGenericTypeDefinition && typeof(AbstractModel).IsAssignableFrom(type)))
		{
			Type[] typeArguments = type.GetGenericArguments();
			foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				if (method.ReturnType != typeof(Task)
					|| !method.IsVirtual
					|| method.GetBaseDefinition().DeclaringType == type)
				{
					continue;
				}

				checkedMethods++;
				string name = $"{type.Name}.{method.Name}";
				if (method.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>() != null)
				{
					violations.Add($"{name}: async 重写(状态机按类型参数实例化)");
				}

				foreach (string reason in FindTypeParameterUses(type, method, typeArguments))
				{
					violations.Add($"{name}: {reason}");
				}
			}
		}

		Expect(checkedMethods >= 6, $"应至少检查到 6 个泛型模型钩子重写,实际 {checkedMethods}");
		Expect(violations.Count == 0, "泛型模型钩子重写直接使用了类型参数:\n" + string.Join("\n", violations));
	}

	private static IEnumerable<string> FindTypeParameterUses(Type owner, MethodInfo method, Type[] typeArguments)
	{
		byte[] il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
		Module module = method.Module;
		int offset = 0;
		while (offset < il.Length)
		{
			short value = il[offset] == 0xFE ? (short)(0xFE00 | il[offset + 1]) : il[offset];
			offset += il[offset] == 0xFE ? 2 : 1;
			OpCode code = IlOpCodes[value];
			int size = code.OperandType switch
			{
				OperandType.InlineNone => 0,
				OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
				OperandType.InlineVar => 2,
				OperandType.InlineI8 or OperandType.InlineR => 8,
				OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
				_ => 4
			};
			if (code.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
			{
				MemberInfo? member = module.ResolveMember(BitConverter.ToInt32(il, offset), typeArguments, null);
				string? reason = DescribeTypeParameterUse(owner, member);
				if (reason != null)
				{
					yield return $"{code.Name} {reason}";
				}
			}

			offset += size;
		}
	}

	private static string? DescribeTypeParameterUse(Type owner, MemberInfo? member)
	{
		switch (member)
		{
		case MethodBase target:
			// 本类的非泛型实例方法/字段:从 this 取泛型上下文,补丁写死实例也不影响。
			if (!target.IsStatic && !target.IsGenericMethod && IsSameGenericClass(owner, target.DeclaringType))
			{
				return null;
			}

			if (target.IsGenericMethod && target is MethodInfo generic
				&& generic.GetGenericArguments().Any(ContainsGenericParameter))
			{
				return $"泛型方法 {target.DeclaringType?.Name}.{target.Name}<{string.Join(",", generic.GetGenericArguments().Select(arg => arg.Name))}>";
			}

			return target.DeclaringType != null && ContainsGenericParameter(target.DeclaringType)
				? $"类型参数实例上的 {target.DeclaringType.Name}.{target.Name}"
				: null;
		case FieldInfo field:
			return !field.IsStatic && IsSameGenericClass(owner, field.DeclaringType)
				? null
				: field.DeclaringType != null && ContainsGenericParameter(field.DeclaringType)
					? $"字段 {field.DeclaringType.Name}.{field.Name}"
					: null;
		case Type type:
			return ContainsGenericParameter(type) ? $"类型 {type.Name}" : null;
		default:
			return null;
		}
	}

	private static bool IsSameGenericClass(Type owner, Type? declaring)
	{
		for (Type? current = owner; current != null; current = current.BaseType)
		{
			if (declaring != null && declaring.IsGenericType && current.IsGenericType
				&& declaring.GetGenericTypeDefinition() == current.GetGenericTypeDefinition())
			{
				return true;
			}

			if (declaring == current)
			{
				return true;
			}
		}

		return false;
	}

	private static bool ContainsGenericParameter(Type type)
	{
		return type.IsGenericParameter
			|| (type.HasElementType && ContainsGenericParameter(type.GetElementType()!))
			|| (type.IsGenericType && type.GetGenericArguments().Any(ContainsGenericParameter));
	}
}
