using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace BetterCharacterRelics;

internal sealed record WatcherRelicSpec(string RelicEntry, string RelicType, string PowerEntry, string PowerType, int Mantra, string DescriptionKey);

internal static class WatcherSupport
{
    internal const string LocalizationFile = "better_character_relics_watcher";

    // 两版都使用 Watcher 程序集名；完整类型及模型 ID 必须同时匹配。
    internal static WatcherRelicSpec[] Specifications =>
    [
        new("WATCHER-PURE_WATER", "Watcher.Code.Relics.PureWater", "WATCHER-MANTRA_POWER", "Watcher.Code.Powers.MantraPower", 2, "pure_water.description"),
        new("WATCHER-HOLY_WATER", "Watcher.Code.Relics.HolyWater", "WATCHER-MANTRA_POWER", "Watcher.Code.Powers.MantraPower", 5, "holy_water.description"),
        new("PURE_WATER", "WatcherMod.PureWater", "MANTRA", "WatcherMod.Mantra", 2, "pure_water.description"),
        new("HOLY_WATER", "WatcherMod.HolyWater", "MANTRA", "WatcherMod.Mantra", 5, "holy_water.description")
    ];

    internal static WatcherRelicSpec? Match(string category, string entry, string? typeName, string? assemblyName)
        => category == "RELIC" && assemblyName == "Watcher"
            ? Specifications.FirstOrDefault(spec => spec.RelicEntry == entry && spec.RelicType == typeName)
            : null;

    internal static WatcherRelicSpec? Match(RelicModel relic)
        => Match(relic.Id.Category, relic.Id.Entry, relic.GetType().FullName, relic.GetType().Assembly.GetName().Name);

    internal static bool HasBaseCombatStartRoute(Type relicType)
        => relicType.GetMethod(nameof(AbstractModel.BeforeCombatStartLate), Type.EmptyTypes)?.DeclaringType == typeof(AbstractModel);

    internal static bool CanGrant(bool hasOwner, bool inCombat, bool dead, bool melted, bool removed, bool usedUp)
        => hasOwner && inCombat && !dead && !melted && !removed && !usedUp;

    internal static PowerModel? ResolveMantra(RelicModel relic, WatcherRelicSpec spec)
    {
        PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(new ModelId("POWER", spec.PowerEntry));
        return power != null && power.GetType().FullName == spec.PowerType
            && power.GetType().Assembly == relic.GetType().Assembly ? power : null;
    }

    internal static async Task GrantAfterOriginal(Task original, RelicModel relic, WatcherRelicSpec spec)
    {
        await original;
        var owner = relic.Owner;
        if (!CanGrant(owner != null, owner?.Creature.CombatState != null, owner?.Creature.IsDead ?? true,
                relic.IsMelted, relic.HasBeenRemovedFromState, relic.IsUsedUp)) return;
        PowerModel? canonical = ResolveMantra(relic, spec);
        if (canonical == null)
        {
            Log.Warn($"[BetterCharacterRelics][Watcher] Cannot resolve {spec.PowerEntry} for {spec.RelicType}; bonus skipped.");
            return;
        }
        // 所有客户端均在原版开战 Hook 中等待此命令；applier 必须是自身，
        // lamali 的真言达到阈值时会检查 applier == Owner。
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), (PowerModel)canonical.ToMutable(),
            owner!.Creature, spec.Mantra, owner.Creature, null);
        RelicEffects.Flash(relic);
    }

    internal static void AddMantraHoverTip(RelicModel relic, ref IEnumerable<IHoverTip> result)
    {
        WatcherRelicSpec? spec = Match(relic);
        if (spec == null || ResolveMantra(relic, spec) is not PowerModel power) return;
        // 部分语言把 Amount 用作进入神格的阈值；遗物提示应解释十层规则。
        result = result.Append(HoverTipFactory.FromPower(power, 10)).ToArray();
    }

    internal static IEnumerable<WatcherRelicSpec> AvailableSpecifications(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies.Where(assembly => assembly.GetName().Name == "Watcher"))
        foreach (WatcherRelicSpec spec in Specifications)
        {
            // 只解析已知名称，不枚举第三方子类，更不会给这些类型安装补丁。
            Type? relicType = assembly.GetType(spec.RelicType, false);
            Type? powerType = assembly.GetType(spec.PowerType, false);
            if (relicType != null && powerType != null && typeof(RelicModel).IsAssignableFrom(relicType)
                && typeof(PowerModel).IsAssignableFrom(powerType) && HasBaseCombatStartRoute(relicType))
                yield return spec;
        }
    }

    internal static void AttachLocalization()
    {
        LocManager manager = LocManager.Instance;
        manager.SubscribeToLocaleChange(() => MergeDescriptions(manager));
        MergeDescriptions(manager);
    }

    internal static void MergeDescriptions(LocManager manager)
    {
        try
        {
            var available = AvailableSpecifications(AppDomain.CurrentDomain.GetAssemblies()).ToArray();
            if (available.Length == 0)
            {
                Log.Info("[BetterCharacterRelics][Watcher] Optional Watcher not loaded; enhancement inactive.");
                return;
            }
            // 原版只自动合并已有表名；独立文案文件从本模组 PCK 读取。
            string path = $"res://BetterCharacterRelics/localization/{manager.Language}/{LocalizationFile}.json";
            if (!Godot.FileAccess.FileExists(path))
                path = $"res://BetterCharacterRelics/localization/eng/{LocalizationFile}.json";
            var source = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString(path))
                ?? throw new InvalidOperationException("Missing Watcher descriptions");
            LocTable relics = manager.GetTable("relics");
            var translations = available.ToDictionary(spec => spec.RelicEntry + ".description",
                spec => source[spec.DescriptionKey], StringComparer.Ordinal);
            relics.MergeWith(translations);
            Log.Info($"[BetterCharacterRelics][Watcher] {manager.Language}: enhanced descriptions for {string.Join(", ", translations.Keys)}.");
        }
        catch (Exception exception)
        {
            Log.Warn($"[BetterCharacterRelics][Watcher] Localization failed: {exception.GetBaseException().Message}");
        }
    }
}

[RelicPatch("watcher.start", "观者初始遗物真言", "Watcher.PureWater|Watcher.HolyWater")]
[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.BeforeCombatStartLate), new Type[] { })]
internal static class WatcherCombatStartPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Normal)]
    internal static void Postfix(AbstractModel __instance, ref Task __result)
    {
        if (__instance is RelicModel relic && WatcherSupport.Match(relic) is WatcherRelicSpec spec
            && WatcherSupport.HasBaseCombatStartRoute(relic.GetType()))
            __result = WatcherSupport.GrantAfterOriginal(__result, relic, spec);
    }
}

[RelicPatch("watcher.localization", "观者强化描述", "Watcher.PureWater|Watcher.HolyWater")]
[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize), new Type[] { })]
internal static class WatcherLocalizationPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Normal)]
    internal static void Postfix() => WatcherSupport.AttachLocalization();
}

[RelicPatch("watcher.hover", "观者真言提示", "Watcher.PureWater|Watcher.HolyWater")]
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTips), MethodType.Getter)]
internal static class WatcherHoverTipsPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Normal)]
    internal static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
        => WatcherSupport.AddMantraHoverTip(__instance, ref __result);
}

[RelicPatch("watcher.event-hover", "观者真言提示", "Watcher.PureWater|Watcher.HolyWater")]
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTipsExcludingRelic), MethodType.Getter)]
internal static class WatcherEventHoverTipsPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Normal)]
    internal static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
        => WatcherSupport.AddMantraHoverTip(__instance, ref __result);
}
