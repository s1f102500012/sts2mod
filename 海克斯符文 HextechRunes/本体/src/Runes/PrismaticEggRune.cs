using System.Globalization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

// 棱彩蛋 —— 你宝箱房内的遗物奖励会被替换为随机的海克斯符文。
// 效果实现在 HextechTreasureRuneHooks:开箱走 TreasureRoomRelicSynchronizer 的
// 「roll→投票选牌位→发放」小游戏,不经过 TryModifyRewards,须在发放前按选位替换。
public sealed class PrismaticEggRune : HextechRelicBase
{

	[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), "BeginRelicPicking")]
	[HextechPatch("rune.prismatic-egg", "棱彩之卵")]
	private static class BeginRelicPickingPatch
	{
		[HarmonyPostfix]
		private static void Postfix(TreasureRoomRelicSynchronizer __instance)
		{
			try
			{
				HextechTreasureRuneHooks.ReplaceRelicsForEggOwners(__instance);
			}
			catch (Exception ex)
			{
				// 替换失败只损失棱彩蛋效果,绝不能打断原版开箱。
				Log.Warn($"[{ModInfo.Id}][Mayhem] PrismaticEgg treasure replacement skipped: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}
