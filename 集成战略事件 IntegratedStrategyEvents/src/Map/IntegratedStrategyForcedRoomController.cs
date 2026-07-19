using IntegratedStrategyEvents.Encounters;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Map;

internal static class IntegratedStrategyForcedRoomController
{
	public static bool HandleCreateRoom(
		RoomType roomType,
		MapPointType mapPointType,
		AbstractModel? model,
		ref AbstractRoom result)
	{
		if (RunManager.Instance.DebugOnlyGetState() is not RunState state)
		{
			return true;
		}

		if (TryCreateForcedPetalEliteRoom(state, roomType, model, out CombatRoom combatRoom))
		{
			result = combatRoom;
			return false;
		}

		if (TryCreateForcedEventRoom(state, roomType, mapPointType, model, out EventRoom eventRoom))
		{
			result = eventRoom;
			return false;
		}

		return true;
	}

	private static bool TryCreateForcedPetalEliteRoom(
		RunState state,
		RoomType roomType,
		AbstractModel? model,
		out CombatRoom room)
	{
		room = null!;
		if (!PetalSpecialEliteNodeController.TryPullForcedEncounter(roomType, out EncounterModel encounter))
		{
			return false;
		}

		LogForcedModelReplacement("Petal special elite node", model, encounter);
		room = new CombatRoom(encounter.ToMutable(), state);
		return true;
	}

	private static bool TryCreateForcedEventRoom(
		RunState state,
		RoomType roomType,
		MapPointType mapPointType,
		AbstractModel? model,
		out EventRoom room)
	{
		room = null!;
		if (roomType != RoomType.Event ||
			!TryGetForcedEventType(state, out Type forcedEventType))
		{
			return false;
		}

		// 原版每幕入口是先古(Ancient)节点，其 Event 房间必须走 PullAncient() 发放
		// 先古祝福；二幕开局分支绝不允许占用它。树洞/终局临时图的起点同为
		// Ancient 节点且依赖本控制器强制事件，故只对开局分支收紧。
		if (mapPointType == MapPointType.Ancient &&
			IntegratedStrategyEventReplay.IsSecondActOpeningBranch(forcedEventType))
		{
			return false;
		}

		// PullNextEvent 内部会走原生 Hook.ModifyNextEvent——其他模组的遗物可以在这里
		// 替换掉我们强制的事件；以 PullNextEvent 的返回值为准（与原版语义一致），
		// 被替换时记录日志，后续访问计数/历史登记按真实事件 id 处理。
		EventModel forcedEvent = state.Act.PullNextEvent(state);
		if (forcedEvent.GetType() != forcedEventType)
		{
			Log.Info(
				$"{ModInfo.LogPrefix} Forced event {forcedEventType.Name} was overridden to " +
				$"{forcedEvent.Id.Entry} by a ModifyNextEvent hook; honoring the override.");
		}

		LogForcedModelReplacement("Forced event node", model, forcedEvent);
		room = new EventRoom(forcedEvent);
		return true;
	}

	private static bool TryGetForcedEventType(RunState state, out Type eventType)
	{
		if (IntegratedStrategyFirstEventPatch.TryGetForcedEventType(state, out eventType))
		{
			return true;
		}

		return IntegratedStrategySecretMapNodeController.TryGetForcedEventType(state, out eventType);
	}

	private static void LogForcedModelReplacement(
		string source,
		AbstractModel? incomingModel,
		AbstractModel forcedModel)
	{
		if (incomingModel != null && !incomingModel.Id.Equals(forcedModel.Id))
		{
			Log.Info(
				$"{ModInfo.LogPrefix} {source} forced room model to " +
				$"{forcedModel.Id.Entry} instead of {incomingModel.Id.Entry}.");
		}
	}
}
