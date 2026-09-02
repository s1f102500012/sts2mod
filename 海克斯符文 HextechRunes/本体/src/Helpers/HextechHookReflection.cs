namespace HextechRunes;

internal static class HextechHookReflection
{
	private static readonly object MissingMemberLogLock = new();
	private static readonly HashSet<string> LoggedMissingMembers = [];
	private static readonly List<string> MissingMemberDescriptions = [];

	/// <summary>启动期发现缺失的原版私有成员(去重),供 <see cref="HextechPatcher.LogSummary"/> 一次性汇总。</summary>
	internal static IReadOnlyList<string> MissingMembers
	{
		get
		{
			lock (MissingMemberLogLock)
			{
				return MissingMemberDescriptions.ToArray();
			}
		}
	}

	public static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
	}

	public static MethodInfo? TryGetMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return TryGetMethod(type, name, flags, warnIfMissing: true, parameters);
	}

	public static MethodInfo? TryGetMethod(
		Type type,
		string name,
		BindingFlags flags,
		bool warnIfMissing,
		params Type[] parameters)
	{
		MethodInfo? method = type.GetMethod(name, flags, binder: null, parameters, modifiers: null);
		if (method == null && warnIfMissing)
		{
			WarnMissingMember(
				$"method:{type.AssemblyQualifiedName}:{name}:{flags}:{string.Join(",", parameters.Select(static parameter => parameter.AssemblyQualifiedName))}",
				$"method {type.FullName}.{name}({string.Join(", ", parameters.Select(static parameter => parameter.FullName ?? parameter.Name))})");
		}

		return method;
	}

	public static FieldInfo RequireField(Type type, string name, BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic)
	{
		return type.GetField(name, flags)
			?? throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
	}

	public static FieldInfo? TryGetField(Type type, string name, BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic)
	{
		return TryGetField(type, name, flags, warnIfMissing: true);
	}

	public static FieldInfo? TryGetField(Type type, string name, BindingFlags flags, bool warnIfMissing)
	{
		FieldInfo? field = type.GetField(name, flags);
		if (field == null && warnIfMissing)
		{
			WarnMissingMember(
				$"field:{type.AssemblyQualifiedName}:{name}:{flags}",
				$"field {type.FullName}.{name}");
		}

		return field;
	}

	public static MethodInfo RequireGetter(Type type, string propertyName, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
	{
		return type.GetProperty(propertyName, flags)?.GetMethod
			?? throw new InvalidOperationException($"Could not find property getter {type.FullName}.{propertyName}.");
	}

	private static void WarnMissingMember(string key, string description)
	{
		lock (MissingMemberLogLock)
		{
			if (!LoggedMissingMembers.Add(key))
			{
				return;
			}

			MissingMemberDescriptions.Add(description);
		}

		Log.Warn($"[{ModInfo.Id}][Reflection] Missing {description}; dependent feature degraded.");
	}
}
