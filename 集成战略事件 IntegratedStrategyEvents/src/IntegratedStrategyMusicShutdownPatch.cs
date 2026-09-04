using HarmonyLib;
using IntegratedStrategyEvents.Encounters;
using IntegratedStrategyEvents.TreeHoles;
using MegaCrit.Sts2.Core.Nodes;

namespace IntegratedStrategyEvents;

// 返回主菜单的两个入口各只登记一个前缀：BOSS 战音乐在前、终局音乐在后，
// 与合并前四个前缀的实际执行序一致；两者都不还原原版音乐，主菜单自己会接管。
internal static class IntegratedStrategyMusicShutdown
{
	internal static void StopAll()
	{
		IntegratedStrategyBossMusic.StopAll();
		IntegratedStrategyEndlessFinaleMusicController.Stop(restoreGameMusic: false);
	}

	[HarmonyPatch(typeof(NGame), nameof(NGame.ReturnToMainMenu))]
	[IntegratedStrategyPatch("IntegratedStrategyMusicReturnToMainMenuPatch", "music", "本模组战斗音乐")]
	internal static class ReturnToMainMenuPatch
	{
		private static void Prefix()
		{
			StopAll();
		}
	}

	[HarmonyPatch(typeof(NGame), nameof(NGame.ReturnToMainMenuAfterRun))]
	[IntegratedStrategyPatch("IntegratedStrategyMusicReturnToMainMenuAfterRunPatch", "music", "本模组战斗音乐")]
	internal static class ReturnToMainMenuAfterRunPatch
	{
		private static void Prefix()
		{
			StopAll();
		}
	}
}
