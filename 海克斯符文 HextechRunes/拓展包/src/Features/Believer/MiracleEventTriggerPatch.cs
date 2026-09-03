using Godot;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

// 「神迹」事件的安全注入点(信徒海克斯)。信徒不在战斗胜利 hook 里直接 EnterRoom —— 那会把刚打赢、奖励还在的战斗房
// 连同流程一起弹掉(EnterRoom 内部先 ExitCurrentRooms),造成「打完 Boss 不自动进下一层要 SL / SL 时事件丢失 / 末战无法结算」。
//
// 改在玩家「战斗领奖屏点继续」时拦截 RunManager.ProceedFromTerminalRewardsScreen —— 此刻战斗房已领奖、流程空闲,是干净交接点。
// 单人 + 栈深 1 + 当前是战斗房 + 非末幕 Boss 且有待触发计数时:消耗一次计数,改为 EnterRoom(神迹)(干净弹掉已领奖的战斗房),
// 并 return false 跳过原方法(原本只是开图)。神迹完成后玩家点 PROCEED,由 NEventRoom.Proceed 独立开图(不重入本补丁)。
//
// SL 安全:计数是 SavedProperty,且在这一刻才于内存里消耗(晚于「战斗胜利后」那次存档)。所以 SL 回到事件中途,会落到
// 「计数仍 >0、战斗房 pre-finished」的存档 → 再次 proceed 时神迹重现,而非消失。末幕 Boss 守卫则避免在终局插事件破坏结算。
// 纯拓展包,主 mod 一行不动。
[SponsorPatch("believer.miracle-trigger", "信徒·神迹事件")]
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class MiracleEventTriggerPatch
{
	// 不再拦截原方法:让它完整执行(关领奖屏、开地图——所有原生 UI 收尾照常),
	// 之后再从"地图已打开"状态 deferred 进神迹,与玩家从地图点进事件房的路径完全同构。
	// (此前的 prefix 替换版把原方法的 UI 收尾一并跳过:领奖层残留拦截输入/事件选项无法点击。)
	[HarmonyPostfix]
	private static void Postfix(RunManager __instance, ref Task __result)
	{
		__result = ChainMiracleAfterProceed(__instance, __result);
	}

	private static async Task ChainMiracleAfterProceed(RunManager runManager, Task original)
	{
		await original;

		try
		{
			if (HextechRelicBase.IsNetworkMultiplayerRun())
			{
				return;
			}

			RunState? state = runManager.DebugOnlyGetState();
			if (state == null || state.CurrentRoomCount != 1)
			{
				return;
			}

			// 只在「战斗领奖→开图」的交接点注入(事件领奖走 NEventRoom.Proceed,不经过这里)。
			if (state.CurrentRoom is not CombatRoom combatRoom)
			{
				return;
			}

			// 末战守卫(bug:末战拿信徒无法结算):末幕 Boss 之后不插事件,留给 EnterNextAct → WinRun 正常结算。
			if (combatRoom.RoomType == RoomType.Boss && state.CurrentActIndex >= state.Acts.Count - 1)
			{
				return;
			}

			BelieverRune? believer = state.Players
				.SelectMany(static player => player.Relics)
				.OfType<BelieverRune>()
				.FirstOrDefault(static relic => relic.HasPendingMiracle);
			if (believer == null)
			{
				return;
			}

			believer.ConsumePendingMiracle();
			// deferred 到下一帧再 EnterRoom:脱离领奖屏 proceed 的协程栈(ExitCurrentRooms 会
			// 销毁正在跑这段代码的战斗 UI 节点),也让地图打开的收尾先落地。
			Callable.From(() =>
			{
				_ = TaskHelper.RunSafely(EnterMiracle(runManager));
			}).CallDeferred();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Miracle trigger postfix error: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}

	private static async Task EnterMiracle(RunManager runManager)
	{
		try
		{
			EventModel miracle = ModelDb.Event<MiracleEvent>();
			// EnterRoomDebug 即 dev console travel 的完整进房管线:ClearScreens(清掉残留的
			// 领奖屏/地图屏——裸 EnterRoom 缺这步,残留领奖按钮层会拦截事件选项点击)、
			// 同步等待、CreateRoom+EnterRoom、FadeIn 过场,与正常进房状态完全一致。
			await runManager.EnterRoomDebug(RoomType.Event, model: miracle, showTransition: true);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] BelieverRune failed to enter Miracle event: {ex.GetType().Name}: {ex.Message}", 2);
			// 兜底:进事件失败时确保地图可用,玩家不至于卡死。
			try
			{
				NMapScreen.Instance?.SetTravelEnabled(true);
				NMapScreen.Instance?.Open();
			}
			catch
			{
			}
		}
	}
}
