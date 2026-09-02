using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 怪物动画机触发安全护栏:原版部分怪物的动画转移条件会无空判硬取 power
/// (花园鳗 GenerateAnimator 的 "Hit" 条件直接 GetPower&lt;SkittishPower&gt;().HasGainedBlockThisTurn),
/// 感受燃烧/升级:暴露剥除该 buff 后,任何受击动画触发都会 NullReferenceException,
/// 异常打穿伤害结算链导致回合卡死(本机日志实证)。动画触发纯属表现层,
/// 这里吞掉 NRE:代价只是该次动画转移不播,战斗流不再被打断。
/// </summary>
internal static class HextechAnimTriggerSafetyHooks
{


	[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger), typeof(string))]
	[HextechPatch("ui.anim-trigger-safety", "动画触发安全")]
	private static class SetAnimationTriggerPatch
	{
		[HarmonyFinalizer]
		private static Exception? Finalizer(Exception? __exception, string trigger)
		{
			if (__exception is not NullReferenceException)
			{
				return __exception;
			}

			if (HextechRunLogBudget.TryConsume("ui.animation-trigger-nre", 5))
			{
				Log.Warn($"[{ModInfo.Id}][AnimSafety] Suppressed NullReferenceException in animation trigger '{trigger}' (likely a vanilla animator condition reading a removed power).");
			}

			return null;
		}
	}
}
