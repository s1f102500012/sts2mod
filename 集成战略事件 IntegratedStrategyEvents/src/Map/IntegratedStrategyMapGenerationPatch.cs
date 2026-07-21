using System.Reflection;
using System.Runtime.CompilerServices;
using IntegratedStrategyEvents.Events;
using IntegratedStrategyEvents.TreeHoles;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Map;

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetNumberOfRooms))]
internal static class IntegratedStrategyMapLengthPatch
{
	private const int ExtraRooms = 1;

	private static void Postfix(ActModel __instance, ref int __result)
	{
		if (__instance.GetType().Assembly == typeof(ActModel).Assembly)
		{
			__result += ExtraRooms;
		}
	}
}

[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.EnsureNextEventIsValid))]
internal static class IntegratedStrategyFirstEventPatch
{
	private const string SecondActOpeningEventRngName = "integrated_strategy_second_act_opening_event";

	private const int SecondActIndex = 1;
	private const int FirstEventBranchCount = 5;
	private static readonly ConditionalWeakTable<RunState, FirstEventChoice> FirstEventChoices = new();

	public static bool TryGetForcedEventType(RunState runState, out Type eventType)
	{
		if (IntegratedStrategyTreeHoleController.IsAtProphetHornFragmentEventPoint(runState))
		{
			eventType = typeof(AnomalousReportEvent);
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsAtEternalDustFirstEventPoint(runState))
		{
			eventType = typeof(ReconstructionEvent);
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsAtEternalDustSecondEventPoint(runState))
		{
			eventType = typeof(ExplorerSmallStepEvent);
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsAtAbyssalJungleSublimationEventPoint(runState))
		{
			eventType = typeof(SublimationEvent);
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsAtAbyssalJungleOdeEventPoint(runState))
		{
			eventType = typeof(OdeEvent);
			return true;
		}

		if (ShouldForceSecondActOpeningEvent(runState))
		{
			eventType = GetSecondActOpeningEventType(runState);
			return true;
		}

		eventType = null!;
		return false;
	}

	private static bool Prefix(RoomSet __instance, RunState runState)
	{
		if (__instance.events.Count == 0)
		{
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsAtProphetHornFragmentEventPoint(runState))
		{
			if (!IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
					__instance,
					runState,
					static eventModel => eventModel is AnomalousReportEvent,
					"saved prophet horn fragment event"))
			{
				int anomalousReportIndex = __instance.eventsVisited % __instance.events.Count;
				RoomSet.SwapToOrCreateAtIndex<EventModel, AnomalousReportEvent>(__instance.events, anomalousReportIndex);
			}

			return false;
		}

		if (IntegratedStrategyTreeHoleController.IsAtEternalDustFirstEventPoint(runState))
		{
			if (!IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
					__instance,
					runState,
					static eventModel => eventModel is ReconstructionEvent,
					"saved Eternal Dust reconstruction event"))
			{
				int reconstructionIndex = __instance.eventsVisited % __instance.events.Count;
				RoomSet.SwapToOrCreateAtIndex<EventModel, ReconstructionEvent>(__instance.events, reconstructionIndex);
			}

			return false;
		}

		if (IntegratedStrategyTreeHoleController.IsAtEternalDustSecondEventPoint(runState))
		{
			if (!IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
					__instance,
					runState,
					static eventModel => eventModel is ExplorerSmallStepEvent,
					"saved Eternal Dust explorer event"))
			{
				int explorerStepIndex = __instance.eventsVisited % __instance.events.Count;
				RoomSet.SwapToOrCreateAtIndex<EventModel, ExplorerSmallStepEvent>(__instance.events, explorerStepIndex);
			}

			return false;
		}

		if (IntegratedStrategyTreeHoleController.IsAtAbyssalJungleSublimationEventPoint(runState))
		{
			if (!IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
					__instance,
					runState,
					static eventModel => eventModel is SublimationEvent,
					"saved Abyssal Jungle sublimation event"))
			{
				int sublimationIndex = __instance.eventsVisited % __instance.events.Count;
				RoomSet.SwapToOrCreateAtIndex<EventModel, SublimationEvent>(__instance.events, sublimationIndex);
			}

			return false;
		}

		if (IntegratedStrategyTreeHoleController.IsAtAbyssalJungleOdeEventPoint(runState))
		{
			if (!IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
					__instance,
					runState,
					static eventModel => eventModel is OdeEvent,
					"saved Abyssal Jungle ode event"))
			{
				int odeIndex = __instance.eventsVisited % __instance.events.Count;
				RoomSet.SwapToOrCreateAtIndex<EventModel, OdeEvent>(__instance.events, odeIndex);
			}

			return false;
		}

		if (IntegratedStrategyTreeHoleController.IsActive(runState) &&
			IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
				__instance,
				runState,
				static _ => true,
				"saved temporary-map event"))
		{
			return false;
		}

		if (IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
				__instance,
				runState,
				IntegratedStrategyEventReplay.IsAnyManagedForcedEvent,
				"saved forced event"))
		{
			return false;
		}

		if (!ShouldForceSecondActOpeningEvent(runState))
		{
			return true;
		}

		int desiredIndex = __instance.eventsVisited % __instance.events.Count;
		FirstEventChoice choice = FirstEventChoices.GetValue(
			runState,
			ChooseFirstEvent);

		if (choice.Branch == FirstEventBranch.Change)
		{
			RoomSet.SwapToOrCreateAtIndex<EventModel, ChangeEvent>(__instance.events, desiredIndex);
			return false;
		}

		if (choice.Branch == FirstEventBranch.PrimordialDivergence)
		{
			RoomSet.SwapToOrCreateAtIndex<EventModel, PrimordialDivergenceEvent>(__instance.events, desiredIndex);
			return false;
		}

		if (choice.Branch == FirstEventBranch.Beginning)
		{
			RoomSet.SwapToOrCreateAtIndex<EventModel, BeginningEvent>(__instance.events, desiredIndex);
			return false;
		}

		if (choice.Branch == FirstEventBranch.Liberation)
		{
			RoomSet.SwapToOrCreateAtIndex<EventModel, LiberationEvent>(__instance.events, desiredIndex);
			return false;
		}

		RoomSet.SwapToOrCreateAtIndex<EventModel, VoidPortentEvent>(__instance.events, desiredIndex);
		return false;
	}

	private static FirstEventChoice ChooseFirstEvent(RunState runState)
	{
		uint seed = IntegratedStrategyStableRng.CreateSeed(
			runState.Rng.Seed,
			SecondActOpeningEventRngName,
			unchecked((uint)runState.CurrentActIndex));
		MegaCrit.Sts2.Core.Random.Rng rng = new(seed, SecondActOpeningEventRngName);
		return new FirstEventChoice((FirstEventBranch)rng.NextInt(FirstEventBranchCount));
	}

	private static Type GetSecondActOpeningEventType(RunState runState)
	{
		FirstEventChoice choice = FirstEventChoices.GetValue(
			runState,
			ChooseFirstEvent);

		return choice.Branch switch
		{
			FirstEventBranch.Change => typeof(ChangeEvent),
			FirstEventBranch.PrimordialDivergence => typeof(PrimordialDivergenceEvent),
			FirstEventBranch.Beginning => typeof(BeginningEvent),
			FirstEventBranch.Liberation => typeof(LiberationEvent),
			_ => typeof(VoidPortentEvent)
		};
	}

	public static bool ShouldForceSecondActOpeningEvent(RunState runState)
	{
		// 秘境节点保留树洞事件；结局分支顺延到二层第一个非秘境事件节点。
		return runState.CurrentActIndex == SecondActIndex &&
			!IntegratedStrategyTreeHoleController.IsActive(runState) &&
			!IntegratedStrategySecretMapNodeController.ShouldSkipMapMutation(runState, runState.Map) &&
			IsAtUnknownMapPoint(runState) &&
			!IntegratedStrategySecretMapNodeController.IsAtSecretNode(runState) &&
			!HasVisitedOrdinaryEventInCurrentAct(runState);
	}

	private static bool IsAtUnknownMapPoint(RunState runState)
	{
		if (!runState.CurrentMapCoord.HasValue)
		{
			return false;
		}

		return runState.Map.GetPoint(runState.CurrentMapCoord.Value) is { PointType: MapPointType.Unknown };
	}

	private static bool HasVisitedOrdinaryEventInCurrentAct(RunState runState)
	{
		// 秘境节点强制的树洞类事件不占用"首个普通事件"额度，
		// 否则先踩到秘境会吞掉结局分支事件。
		return runState.MapPointHistory.Count > runState.CurrentActIndex &&
			runState.MapPointHistory[runState.CurrentActIndex].Any(static entry =>
				entry.MapPointType == MapPointType.Unknown &&
				entry.Rooms.Any(static room => room.RoomType == RoomType.Event &&
					(room.ModelId == null ||
						!IntegratedStrategySecretMapNodeController.IsSecretNodeForcedEventId(room.ModelId))));
	}

	private enum FirstEventBranch
	{
		VoidPortent,
		Change,
		PrimordialDivergence,
		Beginning,
		Liberation
	}

	private sealed record FirstEventChoice(FirstEventBranch Branch);
}

[HarmonyPatch]
internal static class IntegratedStrategyMapPointTypeCountsPatch
{
	private const int ExtraUnknownNodes = 2;

	private static IEnumerable<MethodBase> TargetMethods()
	{
		Type[] actTypes =
		[
			typeof(Overgrowth),
			typeof(Underdocks),
			typeof(Hive),
			typeof(Glory),
			typeof(DeprecatedAct)
		];

		foreach (Type actType in actTypes)
		{
			MethodInfo? method = AccessTools.Method(actType, nameof(ActModel.GetMapPointTypes), [typeof(MegaCrit.Sts2.Core.Random.Rng)]);
			if (method != null)
			{
				yield return method;
			}
		}
	}

	private static void Postfix(ref MapPointTypeCounts __result)
	{
		__result = new MapPointTypeCounts(__result.NumOfUnknowns + ExtraUnknownNodes, __result.NumOfRests)
		{
			NumOfElites = __result.NumOfElites,
			PointTypesThatIgnoreRules = [.. __result.PointTypesThatIgnoreRules]
		};
	}
}

[HarmonyPatch(typeof(UnknownMapPointOdds), nameof(UnknownMapPointOdds.Roll))]
internal static class IntegratedStrategyUnknownRoomOddsPatch
{
	private const float VanillaMonsterOdds = 0.10f;
	private const float VanillaTreasureOdds = 0.02f;
	private const float VanillaShopOdds = 0.03f;

	private const float ModdedMonsterOdds = 0.06f;
	private const float ModdedTreasureOdds = 0.01f;
	private const float ModdedShopOdds = 0.02f;

	private static readonly ConditionalWeakTable<UnknownMapPointOdds, Marker> ConfiguredOdds = new();

	private static void Prefix(UnknownMapPointOdds __instance)
	{
		if (ConfiguredOdds.TryGetValue(__instance, out _))
		{
			return;
		}

		__instance.MonsterOdds = RescaleCurrentOdds(__instance.MonsterOdds, VanillaMonsterOdds, ModdedMonsterOdds);
		__instance.TreasureOdds = RescaleCurrentOdds(__instance.TreasureOdds, VanillaTreasureOdds, ModdedTreasureOdds);
		__instance.ShopOdds = RescaleCurrentOdds(__instance.ShopOdds, VanillaShopOdds, ModdedShopOdds);

		__instance.SetBaseOdds(RoomType.Monster, ModdedMonsterOdds);
		__instance.SetBaseOdds(RoomType.Treasure, ModdedTreasureOdds);
		__instance.SetBaseOdds(RoomType.Shop, ModdedShopOdds);

		ConfiguredOdds.Add(__instance, new Marker());
	}

	private static float RescaleCurrentOdds(float currentOdds, float vanillaBaseOdds, float moddedBaseOdds)
	{
		if (currentOdds < 0f)
		{
			return currentOdds;
		}

		return currentOdds * (moddedBaseOdds / vanillaBaseOdds);
	}

	private sealed class Marker;
}
