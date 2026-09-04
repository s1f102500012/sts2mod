namespace IntegratedStrategyEvents.Events;

public sealed partial class PrimalEntertainmentEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"primal_entertainment.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.Left(1f));

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"原始娱乐",
				new EventPageLoc(
					InitialPage,
					"两只[jitter][red]狂躁[/red][/jitter][gold]美丽[/gold]的生物蹿出草丛，翻滚、踢踹、互相啄食，旁若无物。\n\n本就尖利的脚爪上绑着闪露[aqua]寒芒[/aqua]的刀片，翅膀扇动掀起沙尘和[red]血雾[/red]，飞迸的石子钻进[red]撕裂的伤口[/red]深处。\n\n没有人下注，没有人叫好，这是一场只有你观看的[jitter][red]死斗[/red][/jitter]。\n\n终于，暴虐的演出以一方的[red]死亡[/red]结束，你想离开，胜者却拦在你的面前，它[gold]骄傲[/gold]的眼中还有[red]仇恨[/red]。",
					new EventOptionLoc("FACE_OPPONENT", "直视对方", "我与它同样渴望战斗。"),
					new EventOptionLoc("AVOID_GAZE", "回避视线", "错误的敌意应当回避。")),
				new EventPageLoc(
					"FACE_OPPONENT",
					"你的[jitter][red]原始血性[/red][/jitter]被眼前[gold]美丽的生物[/gold]挑起，你拿起称手的武器，在[sine][purple]循环往复的黑流树海[/purple][/sine]中，[red]死亡[/red]不值得恐惧。",
					new EventOptionLoc("FIGHT", "应战", "遭遇一场特殊的战斗。")),
				new EventPageLoc(
					"AVOID_GAZE",
					"你臣服于它的[jitter][red]暴力[/red][/jitter]，为其让路。它嘶鸣着前行，留下一排[red]染血的爪印[/red]。\n\n它永远无法理解自己脚爪上的武器和心中的愤怒来自何处，但它会去寻找自己的下一个对手，直到与[red]死亡[/red]不期而遇。")),
			new EventLoc(
				"Primal Entertainment",
				new EventPageLoc(
					InitialPage,
					"Two [jitter][red]frenzied[/red][/jitter], [gold]beautiful[/gold] creatures burst from the brush, rolling, kicking, and pecking at each other as though nothing else exists.\n\nBlades gleaming with an [aqua]icy light[/aqua] are bound to their already razor-sharp talons. Their beating wings whip up dust and [red]bloody mist[/red], driving flying stones deep into [red]torn wounds[/red].\n\nNo one places a bet. No one cheers. This is a [jitter][red]fight to the death[/red][/jitter] with you as its sole spectator.\n\nAt last, the savage performance ends in one creature's [red]death[/red]. You turn to leave, but the victor blocks your way, its [gold]proud[/gold] eyes still burning with [red]hatred[/red].",
					new EventOptionLoc("FACE_OPPONENT", "Meet its gaze", "I crave battle as much as it does."),
					new EventOptionLoc("AVOID_GAZE", "Avoid its gaze", "Misplaced hostility is best avoided.")),
				new EventPageLoc(
					"FACE_OPPONENT",
					"The [jitter][red]primal bloodlust[/red][/jitter] within you is stirred by the [gold]beautiful creature[/gold] before you. You take up a familiar weapon. In the [sine][purple]endlessly cycling Blackflow Tree Sea[/purple][/sine], [red]death[/red] is nothing to fear.",
					new EventOptionLoc("FIGHT", "Accept the challenge", "Enter a special combat.")),
				new EventPageLoc(
					"AVOID_GAZE",
					"You yield to its [jitter][red]violence[/red][/jitter] and step aside. It screeches as it moves on, leaving behind a trail of [red]bloodstained clawprints[/red].\n\nIt will never understand where the weapons on its talons or the fury in its heart came from, but it will seek its next opponent until it meets [red]death[/red] by chance."))
		);
	}
}
