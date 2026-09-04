using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace BetterCharacterRelics;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class RelicPatchAttribute(string id, string feature, string content) : Attribute
{
    public string Id { get; } = id;
    public string Feature { get; } = feature;
    public string Content { get; } = content;
    public bool Optional { get; init; }
}

internal sealed record PatchDeclaration(Type Type, RelicPatchAttribute Metadata, MethodInfo Target, MethodInfo Handler, HarmonyMethod Settings);

internal static class Patching
{
    private static bool _installed;
    private static readonly HashSet<string> ReportedConflicts = new(StringComparer.Ordinal);

    internal static IEnumerable<Type> DeclaredTypes => typeof(ModEntry).Assembly.GetTypes()
        .Where(type => type.GetCustomAttribute<RelicPatchAttribute>() != null || type.IsDefined(typeof(HarmonyPatch)))
        .OrderBy(type => type.FullName, StringComparer.Ordinal);

    internal static PatchDeclaration Describe(Type type)
    {
        var metadata = type.GetCustomAttribute<RelicPatchAttribute>()
            ?? throw new InvalidOperationException($"Missing patch metadata: {type.FullName}");
        HarmonyMethod info = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
        if (info.declaringType == null || info.methodName == null)
            throw new InvalidOperationException($"Patch {metadata.Id} has no declarative target");
        string name = info.methodType == MethodType.Getter ? "get_" + info.methodName : info.methodName;
        // 不向基类回退：原版删除 override 时不能意外扩大目标到所有模型。
        MethodInfo? target = info.declaringType.GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly,
            null, info.argumentTypes ?? Type.EmptyTypes, null);
        if (target == null) throw new MissingMethodException(info.declaringType.FullName, name);
        MethodInfo handler = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.IsDefined(typeof(HarmonyPrefix)) || method.IsDefined(typeof(HarmonyPostfix)));
        if (handler.IsDefined(typeof(HarmonyPrefix)) && handler.ReturnType != typeof(bool))
            throw new InvalidOperationException($"Unexpected prefix contract: {metadata.Id}");
        HarmonyMethod settings = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromMethod(handler));
        return new(type, metadata, target, handler, settings);
    }

    internal static void ApplyAll()
    {
        if (_installed) return;
        var harmony = new Harmony(ModEntry.HarmonyId);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int applied = 0, failed = 0;
        foreach (Type type in DeclaredTypes)
        {
            RelicPatchAttribute? meta = type.GetCustomAttribute<RelicPatchAttribute>();
            try
            {
                PatchDeclaration declaration = Describe(type);
                if (!ids.Add(declaration.Metadata.Id)) throw new InvalidOperationException("Duplicate patch ID");
                ValidateBaseRoute(declaration);
                VanillaGuard.Verify(declaration.Target);
                var result = harmony.CreateClassProcessor(type).Patch();
                if (result == null || result.Count != 1) throw new InvalidOperationException("Expected exactly one patched target");
                applied++;
                Log.Info($"[BetterCharacterRelics][Patch] OK {declaration.Metadata.Id}: {VanillaGuard.Key(declaration.Target)}");
            }
            catch (Exception exception)
            {
                failed++;
                string message = $"[BetterCharacterRelics][Patch] FAILED {meta?.Id ?? type.FullName} ({meta?.Feature}): {exception.GetBaseException().Message}";
                if (meta?.Optional == true) Log.Info(message); else Log.Error(message);
            }
        }
        _installed = true;
        Log.Info($"[BetterCharacterRelics][Patch] Applied {applied}/{applied + failed}; failed={failed}; private-member accesses=0.");
        ReportConflicts();
        // 后加载的模组也可能覆盖同一目标；只观察公开加载事件。
        ModManager.OnModDetected += OnModDetected;
    }

    internal static void ValidateBaseRoute(PatchDeclaration declaration)
    {
        if (declaration.Target.DeclaringType != typeof(AbstractModel)) return;
        // 可选观者的真实类型只在它被游戏加载后存在；按完整名称解析并在使用时校验。
        if (declaration.Metadata.Id == "watcher.start") return;
        Type[] consumers = declaration.Metadata.Id switch
        {
            "stars.right" => [typeof(DivineRight)],
            "stars.destiny.entry" => [typeof(DivineDestiny)],
            "rings.discard" => [typeof(RingOfTheSnake), typeof(RingOfTheDrake)],
            _ => throw new InvalidOperationException("Unapproved base-model target")
        };
        foreach (Type consumer in consumers)
        {
            MethodInfo? route = consumer.GetMethod(declaration.Target.Name,
                declaration.Target.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
            if (route == null || route.Module != declaration.Target.Module || route.MetadataToken != declaration.Target.MetadataToken)
                throw new InvalidOperationException($"Base hook route changed for {consumer.Name}.{declaration.Target.Name}");
        }
    }

    private static void OnModDetected(Mod _) => ReportConflicts();

    internal static void ReportConflicts()
    {
        try
        {
            foreach (MethodBase target in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null || !info.Owners.Contains(ModEntry.HarmonyId)) continue;
                string key = VanillaGuard.Key(target);
                foreach (string owner in info.Owners.Where(owner => owner != ModEntry.HarmonyId))
                    if (ReportedConflicts.Add(key + owner))
                        Log.Info($"[BetterCharacterRelics][Conflict] {key} shared with {owner}");
                // 使用 Harmony 的实际拓扑排序，包含 before/after，不能只比较数值优先级。
                var order = PatchProcessor.GetSortedPatchMethods(target, info.Prefixes.ToArray());
                int skipping = order.FindIndex(method => info.Prefixes.Any(patch => patch.PatchMethod == method
                    && patch.owner == ModEntry.HarmonyId && method.ReturnType == typeof(bool)));
                if (skipping < 0) continue;
                foreach (var method in order.Skip(skipping + 1))
                    foreach (var patch in info.Prefixes.Where(patch => patch.PatchMethod == method && patch.owner != ModEntry.HarmonyId))
                        if (ReportedConflicts.Add("skip:" + key + patch.owner + method.Name))
                            Log.Warn($"[BetterCharacterRelics][Conflict] {patch.owner}.{method.Name} follows our skipping prefix on {key}; may be skipped.");
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"[BetterCharacterRelics][Conflict] Scan failed: {exception.GetBaseException().Message}");
        }
    }
}
