namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void EndlessLoopRewindIsDetectedOnlyForRevisitedFirstAct()
	{
		static Func<int, bool> Resolved(params int[] stages) => stage => stages.Contains(stage);

		// Limitless 换章:三幕都发放过,幕序号回到 0。
		Expect(HextechRunLifecycleHooks.IsUnannouncedEndlessLoopRewind(0, 0, Resolved(0, 1, 2)),
			"新一章回到第 0 幕且后续阶段已发放,应识别为未通知的无尽循环");
		// 第一幕中途回地图/开局读档:下一阶段尚未发放。
		Expect(!HextechRunLifecycleHooks.IsUnannouncedEndlessLoopRewind(0, 0, Resolved(0)),
			"第一幕中途或开局读档不应重置");
		// EndlessMode 已先行重置:新循环第 0 幕对应的阶段尚未发放。
		Expect(!HextechRunLifecycleHooks.IsUnannouncedEndlessLoopRewind(0, 3, Resolved(0, 1, 2)),
			"EndlessMode 自行重置后不应重复重置");
		// 已按新循环发放过第 0 幕后再回地图。
		Expect(!HextechRunLifecycleHooks.IsUnannouncedEndlessLoopRewind(0, 3, Resolved(0, 1, 2, 3)),
			"新循环第 0 幕发放后回地图不应再次重置");
		// 只处理拨回第 0 幕;其他幕的阶段关系不作推断。
		Expect(!HextechRunLifecycleHooks.IsUnannouncedEndlessLoopRewind(1, 1, Resolved(0, 1, 2)),
			"非第 0 幕不触发");
		Expect(!HextechRunLifecycleHooks.IsUnannouncedEndlessLoopRewind(0, -1, Resolved(0, 1, 2)),
			"无效阶段序号不触发");
	}
}
