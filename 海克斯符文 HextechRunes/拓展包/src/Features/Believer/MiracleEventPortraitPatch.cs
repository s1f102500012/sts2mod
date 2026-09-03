using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunesSponsorPack;

// 神迹事件立绘:游戏的 EventModel.CreateInitialPortrait() 按 events/{id.entry}.png 自动找图,
// 对自定义事件会解析到不存在的 res://images/events/miracle_event.png。该方法是 public 非 virtual,
// 没法子类 override —— 在拓展包内用 Harmony postfix 拦截:若是 MiracleEvent,改为复用原版
// doors_of_light_and_dark 事件贴图。纯拓展包,不动主 mod。
[SponsorPatch("believer.miracle-portrait", "信徒·神迹事件立绘", Optional = true)]
[HarmonyPatch(typeof(EventModel), nameof(EventModel.CreateInitialPortrait))]
internal static class MiracleEventPortraitPatch
{
	private const string DoorsPortraitPath = "res://images/events/doors_of_light_and_dark.png";

	[HarmonyPostfix]
	private static void Postfix(EventModel __instance, ref Texture2D __result)
	{
		if (__instance is not MiracleEvent)
		{
			return;
		}

		Texture2D? doors = GD.Load<Texture2D>(DoorsPortraitPath);
		if (doors != null)
		{
			__result = doors;
		}
	}
}
