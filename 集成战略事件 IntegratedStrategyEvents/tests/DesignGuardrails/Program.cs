using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using IntegratedStrategyEvents;
using MegaCrit.Sts2.Core.Models;

bool baseline = args.Contains("--baseline");
bool update = args.Contains("--update");
string output = args.First(arg => !arg.StartsWith("--"));
Assembly assembly = typeof(IntegratedStrategyEventsInterop).Assembly;
Type[] types = assembly.GetTypes();
List<string> patches = [], guards = [];
HashSet<string> ids = new(StringComparer.Ordinal);
int checks = 0;
foreach (Type type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
{
	object? metadata = type.GetCustomAttributes().SingleOrDefault(a => a.GetType().Name == "IntegratedStrategyPatchAttribute");
	bool declared = type.IsDefined(typeof(HarmonyPatch), false);
	if (!declared && metadata == null) continue;
	if (!baseline)
	{
		Require(metadata != null && declared, $"incomplete declaration: {type.FullName}");
		string id = Property<string>(metadata!, "Id");
		Require(ids.Add(id), $"duplicate patch ID: {id}");
		Require(!string.IsNullOrWhiteSpace(Property<string>(metadata!, "Feature")) && !string.IsNullOrWhiteSpace(Property<string>(metadata!, "Scope")), $"missing scope: {id}");
	}
	HarmonyMethod target = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
	if (!baseline) Require(type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
		.Any(m => m.Name is "Prefix" or "Postfix" or "Finalizer" || m.GetCustomAttributesData().Any(a => a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyFinalizer")), $"empty patch: {type.FullName}");
	Type[] additional = metadata == null ? [] : Property<Type[]>(metadata, "AdditionalTargets");
	if (baseline && type.Name == "IntegratedStrategyMapPointTypeCountsPatch")
	{
		target.declaringType = typeof(ActModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Acts.Overgrowth")!;
		target.methodName = "GetMapPointTypes";
		target.argumentTypes = [typeof(MegaCrit.Sts2.Core.Random.Rng)];
		additional = new[] { "Underdocks", "Hive", "Glory", "DeprecatedAct" }.Select(name => typeof(ActModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Acts." + name)!).ToArray();
	}
	Require(target.declaringType != null && target.methodName != null, $"missing target: {type.FullName}");
	foreach (Type declaring in new[] { target.declaringType! }.Concat(additional).Distinct())
	{
		MethodInfo? original = AccessTools.Method(declaring, target.methodName, target.argumentTypes);
		Require(original != null, $"unresolved target: {declaring.FullName}.{target.methodName}");
		if (!baseline)
		{
			Require(original!.DeclaringType!.Assembly == typeof(ActModel).Assembly, $"third-party patch: {original}");
			Require(original.DeclaringType.FullName != "MegaCrit.Sts2.Core.Logging.Log" && original.DeclaringType.FullName != "MegaCrit.Sts2.Core.Hooks.Hook", $"forbidden central patch: {original}");
			Require(!(original.DeclaringType == typeof(EventModel) && original.Name is "CreateInitialPortrait" or "GetAssetPaths")
				&& !(original.DeclaringType == typeof(EncounterModel) && original.Name is "CreateBackground" or "GetAssetPaths"), $"use model asset contracts instead: {original}");
		}
		foreach (string kind in new[] { "Prefix", "Postfix", "Finalizer", "Transpiler" })
		{
			MethodInfo? method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
				.SingleOrDefault(m => m.Name == kind || m.GetCustomAttributesData().Any(a => a.AttributeType.Name == "Harmony" + kind));
			if (method == null) continue;
			HarmonyMethod descriptor = new(method);
			int priority = descriptor.priority < 0 ? Priority.Normal : descriptor.priority;
			string key = Key(original!);
			patches.Add($"{key}|{kind}|{type.FullName}.{method.Name}|priority={priority}|before={string.Join(",", descriptor.before ?? [])}|after={string.Join(",", descriptor.after ?? [])}|il={Hash(original!)}");
			if (!baseline) Require(kind != "Transpiler", "transpiler is forbidden");
			if (kind == "Prefix" && method.ReturnType == typeof(bool))
			{
				if (!baseline) Require(priority == Priority.Low, $"skipping prefix is not Low: {type.Name}");
				guards.Add(key + "=" + Hash(original!));
			}
			// 检查 Harmony 私有字段注入的存在与类型，不执行补丁或 Godot 初始化。
			foreach (ParameterInfo parameter in method.GetParameters().Where(p => p.Name!.StartsWith("___")))
			{
				FieldInfo? field = AccessTools.Field(original!.DeclaringType, parameter.Name![3..]);
				Require(field != null, $"missing injected field: {type.Name}.{parameter.Name}");
				Type parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
				Require(parameterType.IsAssignableFrom(field!.FieldType), $"incompatible injected field: {type.Name}.{parameter.Name}");
			}
		}
	}
}

string[] fields = types.Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), false))
	.SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
	.Where(f => !f.IsLiteral && (!f.IsInitOnly || (!f.FieldType.IsValueType && f.FieldType != typeof(string))))
	.Select(f => $"{f.DeclaringType!.FullName}::{f.Name}|{f.FieldType.FullName}|readonly={f.IsInitOnly}").ToArray();
string[] saved = types.SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
	.Where(p => p.GetCustomAttributesData().Any(a => a.AttributeType.Name == "SavedPropertyAttribute"))
	.Select(p => $"{p.DeclaringType!.FullName}::{p.Name}|{p.PropertyType.FullName}").ToArray();
string[] models = types.Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(AbstractModel))).Select(t => t.FullName!).ToArray();

if (!baseline)
{
	Type registry = assembly.GetType("IntegratedStrategyEvents.IntegratedStrategyPrivateMembers", true)!;
	IEnumerable contracts = (IEnumerable)registry.GetProperty("Contracts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
	MethodInfo resolve = registry.GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)!;
	foreach (object contract in contracts)
		Require(resolve.Invoke(null, [contract]) != null, $"private member missing: {contract}");

	// 使用同一套生产代码测试慢端超过旧帧预算和离开跑局的行为。
	Type settlement = assembly.GetType("IntegratedStrategyEvents.TreeHoles.TreeHoleTransitionSettlement", true)!;
	MethodInfo wait = settlement.GetMethod("Await", BindingFlags.NonPublic | BindingFlags.Static)!;
	int frames = 0;
	Task<bool> pending = (Task<bool>)wait.Invoke(null, new object[] {
		(Func<bool>)(() => frames >= 750), (Func<Task>)(() => Task.CompletedTask),
		(Func<Task>)(() => { frames++; return Task.CompletedTask; }), (Func<bool>)(() => true) })!;
	Require(await pending && frames == 750, "event settlement must not advance at 600 local frames");
	Task<bool> cancelled = (Task<bool>)wait.Invoke(null, new object[] {
		(Func<bool>)(() => false), (Func<Task>)(() => Task.CompletedTask),
		(Func<Task>)(() => Task.CompletedTask), (Func<bool>)(() => false) })!;
	Require(!await cancelled, "leaving the run must cancel settlement without state mutation");
	frames = 0;
	TaskCompletionSource abandonedOption = new();
	Task<bool> abandoned = (Task<bool>)wait.Invoke(null, new object[] {
		(Func<bool>)(() => false), (Func<Task>)(() => abandonedOption.Task),
		(Func<Task>)(() => { frames++; return Task.CompletedTask; }), (Func<bool>)(() => frames < 3) })!;
	Require(!await abandoned && frames == 3, "run cancellation must not await an unfinished remote option");
	abandonedOption.SetResult();
	Type lifecycle = assembly.GetType("IntegratedStrategyEvents.Map.IntegratedStrategyMapLifecycle", true)!;
	Require(lifecycle.GetMethod("ModifyGeneratedMapLate")!.DeclaringType == lifecycle, "save-load restoration must use the late map hook");
	Require(lifecycle.GetMethod("AfterMapGenerated")!.DeclaringType == lifecycle, "secret nodes must use model callbacks");
	// 变身池的状态机探测：第三方类型不得触发探测，同一类型只探测一次。
	Type izumikOffspring = assembly.GetType("IntegratedStrategyEvents.Encounters.IzumikOffspring", true)!;
	MethodInfo isUnsafe = izumikOffspring.GetMethod("IsUnsafeMoveStateMachine", BindingFlags.Static | BindingFlags.NonPublic)!;
	Func<bool> mustNotProbe = () => throw new InvalidOperationException("probed a type that must be rejected outright");
	Require((bool)isUnsafe.Invoke(null, [izumikOffspring, mustNotProbe])!, "third-party monster types must be unsafe without probing them");
	Type vanillaMonster = typeof(MonsterModel);
	int probes = 0;
	Func<bool> countingProbe = () => { probes++; return false; };
	Require(!(bool)isUnsafe.Invoke(null, [vanillaMonster, countingProbe])! && probes == 1, "vanilla monster types must be probed once");
	Require(!(bool)isUnsafe.Invoke(null, [vanillaMonster, mustNotProbe])! && probes == 1, "repeated probes of the same type must hit the cache");
	Type calendarKings = assembly.GetType("IntegratedStrategyEvents.Encounters.CalendarKingsPincerBossEncounter", true)!;
	Require(calendarKings.GetMethod("BuildProgrammaticCombatBackground", BindingFlags.Instance | BindingFlags.NonPublic)!.DeclaringType == calendarKings,
		"calendar kings background must be supplied by its model");
	if (!update)
	{
		using Stream? embedded = assembly.GetManifestResourceStream("vanilla_guard.txt");
		Require(embedded != null, "missing embedded vanilla guard");
		using StreamReader reader = new(embedded!);
		Require((await reader.ReadToEndAsync()).Replace("\r\n", "\n") == string.Join('\n', guards.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) + "\n", "embedded guard differs from target IL");
	}
}

Verify("patches.txt", patches);
Verify("mutable_statics.txt", fields);
Verify("saved_properties.txt", saved);
Verify("models.txt", models);
Verify("vanilla_guard.txt", guards.Distinct(StringComparer.Ordinal));
string? previous = args.FirstOrDefault(arg => arg.StartsWith("--previous="))?[11..];
if (!baseline && previous != null)
{
	Require(File.ReadAllLines(Path.Combine(previous, "saved_properties.txt")).Order(StringComparer.Ordinal).SequenceEqual(saved.Order(StringComparer.Ordinal)), "saved properties changed from pre-refactor baseline");
	string[] previousModels = File.ReadAllLines(Path.Combine(previous, "models.txt"));
	Require(!previousModels.Except(models).Any(), "pre-existing model removed");
	Require(models.Except(previousModels).SequenceEqual(new[] { "IntegratedStrategyEvents.Map.IntegratedStrategyMapLifecycle" }), "unexpected model addition");
	string[] previousPatches = File.ReadAllLines(Path.Combine(previous, "patches.txt")).Select(line => string.Join('|', line.Split('|').Take(3))).ToArray();
	string[] currentPatches = patches.Select(line => string.Join('|', line.Split('|').Take(3))).ToArray();
	Require(!currentPatches.Except(previousPatches).Any(), "unexpected patch target or implementation added");
	string[] removed = previousPatches.Except(currentPatches).Select(line => line.Split('|')[2]).Order(StringComparer.Ordinal).ToArray();
	Require(removed.SequenceEqual(new[] {
		"IntegratedStrategyEvents.Encounters.CalendarKingsPincerCreateBackgroundPatch.UseTheInsatiableBackground",
		"IntegratedStrategyEvents.Encounters.CalendarKingsPincerGetAssetPathsPatch.AddTheInsatiableBackgroundAssetPaths",
		"IntegratedStrategyEvents.Events.IntegratedStrategyEventAssetPathsPatch.Postfix",
		"IntegratedStrategyEvents.Events.IntegratedStrategyEventCreateInitialPortraitPatch.Prefix",
		"IntegratedStrategyEvents.Map.IntegratedStrategySecretMapNodeGenerationPatch.Prefix",
		"IntegratedStrategyEvents.Map.IntegratedStrategyTreeHoleEarlyRestorePatch.Prefix" }), "unexpected removed patch");
}
Console.WriteLine($"Design guardrails passed: {checks} assertions, {patches.Count} patch entries, {saved.Length} saved properties, {models.Length} models. Mode={(baseline ? "baseline" : update ? "record" : "verify")}");

void Require(bool condition, string message)
{
	checks++;
	if (!condition) throw new InvalidOperationException(message);
}
void Verify(string file, IEnumerable<string> lines)
{
	string content = string.Join('\n', lines.Order(StringComparer.Ordinal)) + "\n";
	string path = Path.Combine(output, file);
	if (update || baseline)
	{
		Directory.CreateDirectory(output);
		File.WriteAllText(path, content);
	}
	else Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == content, $"snapshot drift: {path}");
}
static T Property<T>(object instance, string name) => (T)instance.GetType().GetProperty(name)!.GetValue(instance)!;
static string Key(MethodBase method) => $"{method.DeclaringType!.FullName}::{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName))})";
static string Hash(MethodBase method)
{
	Type? state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType ?? method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
	byte[] body = method.GetMethodBody()?.GetILAsByteArray() ?? [];
	byte[] continuation = state?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetMethodBody()?.GetILAsByteArray() ?? [];
	return Convert.ToHexString(SHA256.HashData([.. body, .. continuation])).ToLowerInvariant();
}
