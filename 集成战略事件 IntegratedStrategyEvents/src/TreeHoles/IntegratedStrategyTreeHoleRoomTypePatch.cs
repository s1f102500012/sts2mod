using HarmonyLib;
using IntegratedStrategyEvents.Map;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleRoomTypePatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyTreeHoleRoomTypePatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(MapPointType pointType, ref RoomType __result)
	{
		if (pointType != MapPointType.Unknown)
		{
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsActiveCurrentRun())
		{
			__result = RoomType.Event;
			Log.Info($"{ModInfo.LogPrefix} Forced tree-hole unknown node to resolve as an Event room.");
			return false;
		}

		if (IntegratedStrategySecretMapNodeController.IsAtSecretNodeCurrentRun())
		{
			__result = RoomType.Event;
			Log.Info($"{ModInfo.LogPrefix} Forced secret map node to resolve as an Event room.");
			return false;
		}

		return true;
	}
}
