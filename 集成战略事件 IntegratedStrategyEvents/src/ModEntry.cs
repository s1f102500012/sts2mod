using System.Reflection;
using IntegratedStrategyEvents.Cards;
using IntegratedStrategyEvents.Encounters;
using IntegratedStrategyEvents.Events;
using IntegratedStrategyEvents.Map;
using IntegratedStrategyEvents.Relics;
using IntegratedStrategyEvents.TreeHoles;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Content;

namespace IntegratedStrategyEvents;

[ModInitializer(nameof(Initialize))]
public static partial class ModEntry
{
	private static Harmony? HarmonyInstance;
	private static bool CardPoolsRegistered;

	public static void Initialize()
	{
		IntegratedStrategyTreeHoleSaveStateStore.Initialize();
		InjectSavedPropertyCaches();
		RegisterRelics();
		RegisterCards();
		RegisterActEvents();
		EnsureModelsRegisteredIfModelDbAlreadyInitialized();
		HarmonyInstance ??= new Harmony(ModInfo.HarmonyId);
		IntegratedStrategyPrivateMembers.Validate();
		IntegratedStrategyPatcher.ApplyAll(HarmonyInstance, Assembly.GetExecutingAssembly());
		IntegratedStrategyEncounterLocalization.Install();
		IntegratedStrategyEventRuntimeCompatibility.Install();
		IntegratedStrategyPotionTracker.Install();
		IntegratedStrategyTreeHoleController.Install();
		IntegratedStrategyPatcher.DumpIfRequested();
		Log.Info($"{ModInfo.LogPrefix} Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}.");
	}


	private static void RegisterRelics()
	{
		foreach (Type relicType in IntegratedStrategyContentCatalog.EventRelicTypes)
		{
			ModHelper.AddModelToPool(typeof(EventRelicPool), relicType);
		}
	}

	private static void RegisterCards()
	{
		if (CardPoolsRegistered)
		{
			return;
		}

		CardPoolsRegistered = true;
		try
		{
			ModHelper.AddModelToPool(typeof(TokenCardPool), typeof(Frozen));
		}
		catch (InvalidOperationException ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not add Frozen to TokenCardPool: {ex.Message}");
		}
	}

	private static void RegisterActEvents()
	{
		// 与 BaseLib 时代语义对齐：有章节限定的事件进对应章节池，
		// 其余事件一律进通用池（AllSharedEvents，等价原版滑脚木桥式全局事件）；
		// 树洞/结局分支/终局专属事件同样在通用池里，由 IsAllowed 门禁阻止自然刷新，
		// 强制刷新路径不受门禁影响。
		ModContentRegistry registry = ModContentRegistry.For(ModInfo.ModId);
		registry.RegisterSingleton<IntegratedStrategyMapLifecycle>();
		foreach ((Type eventType, Type[] actTypes) in IntegratedStrategyEventSpawnRules.ActRegistrations)
		{
			foreach (Type actType in actTypes)
			{
				registry.RegisterActEvent(actType, eventType);
			}
		}

		foreach (Type eventType in IntegratedStrategyContentCatalog.EventTypes)
		{
			if (!IntegratedStrategyEventSpawnRules.ActRegistrations.ContainsKey(eventType))
			{
				registry.RegisterSharedEvent(eventType);
			}
		}
	}

	private static void EnsureModelsRegisteredIfModelDbAlreadyInitialized()
	{
		if (!ModelDb.Contains(typeof(Ironclad)))
		{
			return;
		}

		EnsureModelsRegistered();
	}

	// BaseLib 时代由其 ModelDb 补丁负责注入自定义模型；现在改为本模组自行保证：
	// 初始化时若 ModelDb 已就绪立即注入，否则由 ModelDb.InitIds 的 postfix 兜底。
	internal static void EnsureModelsRegistered()
	{
		foreach (Type type in IntegratedStrategyContent.ModelTypes)
		{
			if (ModelDb.Contains(type))
			{
				continue;
			}

			ModelDb.Inject(type);
			ModelId id = ModelDb.GetId(type);
			ModelDb.GetById<AbstractModel>(id).InitId(id);
		}
	}
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.InitIds))]
[IntegratedStrategyPatch("IntegratedStrategyModelDbInitIdsPatch", "content", "本模组内容")]
internal static class IntegratedStrategyModelDbInitIdsPatch
{
	private static void Postfix()
	{
		ModEntry.EnsureModelsRegistered();
	}
}
